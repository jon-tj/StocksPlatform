import { Component, OnInit, OnDestroy, inject } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { forkJoin, Subject } from 'rxjs';
import { debounceTime, distinctUntilChanged, filter, switchMap } from 'rxjs/operators';
import { DecimalPipe, TitleCasePipe, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AssetService, AssetDetails, AssetDelta, HoldingDelta } from '../../services/asset.service';
import { PositionsService, Position } from '../../services/positions.service';
import { StockChart, PriceSeries } from '../../components/stock-chart/stock-chart';

export interface ChildRow {
  position: Position;
  holding: HoldingDelta | null;
}

@Component({
  selector: 'app-analysis',
  imports: [RouterLink, DecimalPipe, TitleCasePipe, DatePipe, StockChart, FormsModule],
  templateUrl: './analysis.html',
  styleUrl: './analysis.css',
})
export class Analysis implements OnInit, OnDestroy {
  private route = inject(ActivatedRoute);
  private assetService = inject(AssetService);
  private positionsService = inject(PositionsService);

  assetId: string | null = null;
  asset: AssetDetails | null = null;
  delta: AssetDelta | null = null;
  private latestDelta: AssetDelta | null = null;
  children: ChildRow[] = [];
  holdingsFilter = '';
  chartSeries: PriceSeries[] = [];
  loading = true;
  error: string | null = null;

  get filteredChildren(): ChildRow[] {
    const q = this.holdingsFilter.trim().toLowerCase();
    if (!q) return this.children;
    return this.children.filter(row =>
      row.position.symbol?.toLowerCase().includes(q) ||
      row.position.name?.toLowerCase().includes(q)
    );
  }

  protected brokerUrl: string | null = null;

  // Maps "MMM d" label → ISO date string for the hover→delta lookup
  private timeLabelToDate = new Map<string, string>();
  hoveredDateLabel: string | null = null;
  private hoverDate$ = new Subject<string | null>();
  private hoverSub = this.hoverDate$.pipe(
    filter((label): label is string => label !== null), // never debounce leaves
    debounceTime(150),
    distinctUntilChanged(),
    switchMap(label => {
      if (!this.assetId) return [null];
      const iso = this.timeLabelToDate.get(label);
      if (!iso) return [null];
      return this.assetService.getDeltaAt(this.assetId, iso);
    }),
  ).subscribe(result => {
    // Guard: if mouse has already left while the request was in-flight, discard
    if (this.hoveredDateLabel) {
      this.delta = result;
    }
  });

  onChartDateHover(label: string | null): void {
    this.hoveredDateLabel = label;
    if (!label) {
      // Mouse left chart — skip the debounce and restore immediately
      this.delta = this.latestDelta;
    }
    this.hoverDate$.next(label);
  }

  ngOnInit() {
    this.assetId = this.route.snapshot.paramMap.get('asset');
    if (!this.assetId) {
      this.error = 'No asset specified.';
      this.loading = false;
      return;
    }

    const oneYearAgo = new Date();
    oneYearAgo.setFullYear(oneYearAgo.getFullYear() - 1);

    forkJoin({
      asset: this.assetService.getAssetDetails(this.assetId),
      delta: this.assetService.getLatestDelta(this.assetId),
      history: this.assetService.getHistory(this.assetId, oneYearAgo),
    }).subscribe({
      next: ({ asset, delta, history }) => {
        this.asset = asset;
        if (asset.broker?.toLowerCase().includes('nordnet')) {
          this.brokerUrl = `https://www.nordnet.no/aksjer/kurser/${asset.brokerSymbol}`;
        }
        this.delta = delta;
        this.latestDelta = delta;
        this.chartSeries = [{ name: asset.name, prices: history.prices, times: history.times }];

        // Build label→ISO map using the same date window as the history fetch
        const times = history.times;
        const startDate = new Date(oneYearAgo);
        // Walk trading days to align labels (times skips weekends; we match by index)
        let dayCount = 0;
        for (let d = new Date(startDate); dayCount < times.length; d.setDate(d.getDate() + 1)) {
          if (d.getDay() === 0 || d.getDay() === 6) continue;
          const iso = d.toISOString().split('T')[0];
          this.timeLabelToDate.set(times[dayCount], iso);
          dayCount++;
        }

        if (asset.type === 'Portfolio') {
          forkJoin({
            resp: this.positionsService.getPositions(),
            holdings: this.assetService.getHoldings(this.assetId!),
          }).subscribe({
            next: ({ resp, holdings }) => {
              const holdingMap = new Map(holdings.map((h) => [h.assetId, h]));
              this.children = resp.positions.map((p) => ({
                position: p,
                holding: holdingMap.get(p.assetId) ?? null,
              }));
              this.loading = false;
            },
            error: () => { this.loading = false; },
          });
        } else {
          this.loading = false;
        }
      },
      error: () => {
        this.error = 'Failed to load asset data.';
        this.loading = false;
      },
    });
  }

  ngOnDestroy(): void {
    this.hoverSub.unsubscribe();
    this.hoverDate$.complete();
  }
}
