import { Component, OnInit, OnDestroy, inject } from '@angular/core';
import { Title } from '@angular/platform-browser';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Subscription, forkJoin, Subject, interval } from 'rxjs';
import { debounceTime, distinctUntilChanged, filter, switchMap, startWith } from 'rxjs/operators';
import { DecimalPipe, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import {
  AssetService,
  AssetDetails,
  AssetDelta,
  HoldingDelta,
  FundHoldingSnapshot,
  LivePrice,
} from '../../services/asset.service';
import { PositionsService, Position } from '../../services/positions.service';
import { StockChart, PriceSeries } from '../../components/stock-chart/stock-chart';
import { SectorLabelPipe } from '../../pipes/sector-label.pipe';
import { AssetChip } from '../../components/asset-chip/asset-chip';

export interface ChildRow {
  position: Position;
  holding: HoldingDelta | null;
}

export type DeltaMetricKey =
  | 'market'
  | 'public-sentiment'
  | 'member-sentiment'
  | 'fundamental'
  | 'institutional-order-flow'
  | 'pattern';

@Component({
  selector: 'app-analysis',
  imports: [DecimalPipe, DatePipe, StockChart, FormsModule, SectorLabelPipe, RouterLink, AssetChip],
  templateUrl: './analysis.html',
  styleUrl: './analysis.css',
})
export class Analysis implements OnInit, OnDestroy {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private assetService = inject(AssetService);
  private positionsService = inject(PositionsService);
  private titleService = inject(Title);

  assetId: string | null = null;
  asset: AssetDetails | null = null;
  delta: AssetDelta | null = null;
  private latestDelta: AssetDelta | null = null;
  children: ChildRow[] = [];
  holdingsFilter = '';
  chartSeries: PriceSeries[] = [];
  loading = true;
  recomputing = false;
  starring = false;
  isStarred = false;
  error: string | null = null;
  selectedMetric: DeltaMetricKey | null = null;
  detailsLoading = false;
  institutionalSnapshots: FundHoldingSnapshot[] = [];
  livePrice: LivePrice | null = null;
  liveByAssetId = new Map<string, LivePrice>();

  private liveSub: Subscription | null = null;

  get filteredChildren(): ChildRow[] {
    const q = this.holdingsFilter.trim().toLowerCase();
    if (!q) return this.children;
    return this.children.filter(row =>
      row.position.symbol?.toLowerCase().includes(q) ||
      row.position.name?.toLowerCase().includes(q)
    );
  }

  protected tickerUrl: string | null = null;

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
  private routeSub: Subscription | null = null;

  onChartDateHover(label: string | null): void {
    this.hoveredDateLabel = label;
    if (!label) {
      // Mouse left chart — skip the debounce and restore immediately
      this.delta = this.latestDelta;
    }
    this.hoverDate$.next(label);
  }

  ngOnInit() {
    this.routeSub = this.route.paramMap.subscribe((params) => {
      const nextAssetId = params.get('asset');
      if (!nextAssetId) {
        this.error = 'No asset specified.';
        this.loading = false;
        return;
      }

      if (nextAssetId === this.assetId) {
        return;
      }

      this.assetId = nextAssetId;
      this.loadAsset(this.assetId);
    });
  }

  private loadAsset(assetId: string): void {
    this.loading = true;
    this.error = null;
    this.asset = null;
    this.delta = null;
    this.latestDelta = null;
    this.children = [];
    this.chartSeries = [];
    this.holdingsFilter = '';
    this.tickerUrl = null;
    this.hoveredDateLabel = null;
    this.timeLabelToDate.clear();
    this.selectedMetric = null;
    this.detailsLoading = false;
    this.institutionalSnapshots = [];
    this.isStarred = false;
    this.starring = false;
    this.livePrice = null;
    this.liveByAssetId.clear();

    const oneYearAgo = new Date();
    oneYearAgo.setFullYear(oneYearAgo.getFullYear() - 1);

    forkJoin({
      asset: this.assetService.getAssetDetails(assetId),
      delta: this.assetService.getLatestDelta(assetId),
      history: this.assetService.getHistory(assetId, oneYearAgo),
      starred: this.assetService.isAssetStarred(assetId),
    }).subscribe({
      next: ({ asset, delta, history, starred }) => {
        this.asset = asset;
        this.isStarred = starred;
        this.titleService.setTitle(`${asset.name} | StocksPlatform`);
        if (asset.broker?.toLowerCase().includes('nordnet') && asset.brokerSymbol) {
          this.tickerUrl = `https://www.nordnet.no/aksjer/kurser/${asset.brokerSymbol}`;
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
            holdings: this.assetService.getHoldings(assetId),
          }).subscribe({
            next: ({ resp, holdings }) => {
              const holdingMap = new Map(holdings.map((h) => [h.assetId, h]));
              this.children = resp.positions.map((p) => ({
                position: p,
                holding: holdingMap.get(p.assetId) ?? null,
              }));
              this.startLivePriceStream(assetId, this.children.map(c => c.position.assetId));
              this.loading = false;
            },
            error: () => { this.loading = false; },
          });
        } else {
          this.startLivePriceStream(assetId, []);
          this.loading = false;
        }
      },
      error: () => {
        this.error = 'Failed to load asset data.';
        this.loading = false;
      },
    });
  }

  toggleStar(): void {
    if (!this.assetId || this.starring) return;

    this.starring = true;
    const op = this.isStarred
      ? this.assetService.unstarAsset(this.assetId)
      : this.assetService.starAsset(this.assetId);

    op.subscribe({
      next: () => {
        this.isStarred = !this.isStarred;
        this.starring = false;
      },
      error: () => {
        this.starring = false;
      },
    });
  }

  getHoldingLivePrice(assetId: string): LivePrice | null {
    return this.liveByAssetId.get(this.normalizeAssetId(assetId)) ?? null;
  }

  private startLivePriceStream(primaryAssetId: string, holdingIds: string[]): void {
    this.liveSub?.unsubscribe();

    const normalizedPrimaryId = this.normalizeAssetId(primaryAssetId);
    const ids = [
      normalizedPrimaryId,
      ...holdingIds
        .map(id => this.normalizeAssetId(id))
        .filter(id => id !== normalizedPrimaryId),
    ];
    if (ids.length === 0) return;

    this.liveSub = interval(15000).pipe(
      startWith(0),
      switchMap(() => this.assetService.getLivePrices(ids)),
    ).subscribe({
      next: (rows) => {
        this.liveByAssetId = new Map(
          rows.map(r => [this.normalizeAssetId(r.assetId), r])
        );
        this.livePrice = this.liveByAssetId.get(normalizedPrimaryId) ?? null;
      },
      error: () => { },
    });
  }

  private normalizeAssetId(assetId: string): string {
    return assetId.trim().toLowerCase();
  }

  onRefreshDeltas(): void {
    if (!this.assetId || this.recomputing) return;
    this.recomputing = true;

    if (this.asset?.type === 'Portfolio')
    {
      forkJoin({
        delta: this.assetService.refreshLatestDelta(this.assetId),
        resp: this.positionsService.getPositions(),
        holdings: this.assetService.getHoldings(this.assetId),
      }).subscribe({
        next: ({ delta, resp, holdings }) => {
          this.delta = delta;
          this.latestDelta = delta;

          var holdingMap = new Map(holdings.map((h) => [h.assetId, h]));
          this.children = resp.positions.map((p) => ({
            position: p,
            holding: holdingMap.get(p.assetId) ?? null,
          }));

          this.recomputing = false;
        },
        error: () => { this.recomputing = false; },
      });

      return;
    }

    this.assetService.refreshLatestDelta(this.assetId).subscribe({
      next: (delta) => {
        this.delta = delta;
        this.latestDelta = delta;
        this.recomputing = false;
      },
      error: () => { this.recomputing = false; },
    });
  }

  selectMetric(metric: DeltaMetricKey): void {
    if (!this.assetId) return;

    if (this.selectedMetric === metric) {
      this.clearMetricSelection();
      return;
    }

    this.selectedMetric = metric;

    if (metric !== 'institutional-order-flow') {
      this.detailsLoading = false;
      this.institutionalSnapshots = [];
      return;
    }

    this.detailsLoading = true;
    this.assetService.getInstitutionalSnapshots(this.assetId).subscribe({
      next: (rows) => {
        this.institutionalSnapshots = rows;
        this.detailsLoading = false;
      },
      error: () => {
        this.institutionalSnapshots = [];
        this.detailsLoading = false;
      },
    });
  }

  clearMetricSelection(): void {
    this.selectedMetric = null;
    this.detailsLoading = false;
    this.institutionalSnapshots = [];
  }

  isMetricSelected(metric: DeltaMetricKey): boolean {
    return this.selectedMetric === metric;
  }

  openChildAnalysis(assetId: string): void {
    this.router.navigate(['/analysis', assetId]);
  }

  ngOnDestroy(): void {
    this.routeSub?.unsubscribe();
    this.liveSub?.unsubscribe();
    this.hoverSub.unsubscribe();
    this.hoverDate$.complete();
  }
}
