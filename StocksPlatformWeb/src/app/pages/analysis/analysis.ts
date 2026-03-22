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
  PublicSentiment,
} from '../../services/asset.service';
import { PositionsService, Position } from '../../services/positions.service';
import { StockChart, PriceSeries } from '../../components/stock-chart/stock-chart';
import { SectorLabelPipe } from '../../pipes/sector-label.pipe';
import { AssetChip } from '../../components/asset-chip/asset-chip';
import { ValueChip } from '../../components/value-chip/value-chip';
import { GainChip } from '../../components/gain-chip/gain-chip';

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

export type SortCol =
  | 'name' | 'sector'
  | 'market' | 'fundamental' | 'public-sentiment' | 'member-sentiment'
  | 'inst-flow' | 'pattern' | 'score' | 'target' | 'current';

export type AggregateBy = 'asset' | 'sector' | 'region';

export type AggSortCol = 'label' | 'count' | 'market' | 'fundamental' | 'public-sentiment' | 'member-sentiment' | 'inst-flow' | 'pattern' | 'score' | 'target' | 'current';

export type OrderMode = 'buy' | 'update';

export interface OrderRow {
  assetId: string;
  name: string;
  symbol: string;
  iconUrl?: string;
  price: number;
  targetPct: number;
  total: number;
  positionValue: number;
  current: number;
  delta: number;
}

export interface AggregateRow {
  label: string;
  count: number;
  iconUrls: string[];
  market: number;
  fundamental: number;
  publicSentiment: number;
  memberSentiment: number;
  instFlow: number;
  pattern: number;
  score: number;
  targetFraction: number;
  currentFraction: number;
}

@Component({
  selector: 'app-analysis',
  imports: [DecimalPipe, DatePipe, StockChart, FormsModule, SectorLabelPipe, RouterLink, AssetChip, ValueChip, GainChip],
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
  sortCol: SortCol | null = 'score';
  sortDir: 'asc' | 'desc' = 'desc';
  aggregateBy: AggregateBy = 'asset';
  aggSortCol: AggSortCol = 'score';
  aggSortDir: 'asc' | 'desc' = 'desc';
  showOrderHelper = false;
  orderMode: OrderMode = 'buy';
  orderMoney = 0;
  orderSaving = false;
  hideZeroDelta = false;
  portfolioRemainder = 0;
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
  sentiment: PublicSentiment | null = null;
  sentimentLoading = false;

  private liveSub: Subscription | null = null;

  get filteredChildren(): ChildRow[] {
    const q = this.holdingsFilter.trim().toLowerCase();
    const filtered = q
      ? this.children.filter(row =>
          row.position.symbol?.toLowerCase().includes(q) ||
          row.position.name?.toLowerCase().includes(q) ||
          row.position.sector?.toLowerCase().includes(q)
        )
      : this.children;

    if (!this.sortCol) return filtered;

    const col = this.sortCol;
    const dir = this.sortDir === 'asc' ? 1 : -1;

    return [...filtered].sort((a, b) => {
      let av: string | number | null | undefined;
      let bv: string | number | null | undefined;

      switch (col) {
        case 'name':    av = a.position.name;   bv = b.position.name;   break;
        case 'sector':  av = a.position.sector; bv = b.position.sector; break;
        case 'market':  av = a.holding?.marketDelta; bv = b.holding?.marketDelta; break;
        case 'fundamental': av = a.holding?.fundamentalDelta; bv = b.holding?.fundamentalDelta; break;
        case 'public-sentiment': av = a.holding?.publicSentimentDelta; bv = b.holding?.publicSentimentDelta; break;
        case 'member-sentiment': av = a.holding?.memberSentimentDelta; bv = b.holding?.memberSentimentDelta; break;
        case 'inst-flow': av = a.holding?.institutionalOrderFlowDelta; bv = b.holding?.institutionalOrderFlowDelta; break;
        case 'pattern': av = a.holding?.patternDelta; bv = b.holding?.patternDelta; break;
        case 'score':   av = a.holding?.combinedScore; bv = b.holding?.combinedScore; break;
        case 'target':  av = a.holding?.targetFraction; bv = b.holding?.targetFraction; break;
        case 'current': av = a.position.fraction; bv = b.position.fraction; break;
      }

      if (av == null && bv == null) return 0;
      if (av == null) return 1;  // nulls last
      if (bv == null) return -1;
      if (typeof av === 'string' && typeof bv === 'string')
        return dir * av.localeCompare(bv);
      return dir * ((av as number) - (bv as number));
    });
  }

  toggleSort(col: SortCol): void {
    if (this.sortCol === col) {
      this.sortDir = this.sortDir === 'asc' ? 'desc' : 'asc';
    } else {
      this.sortCol = col;
      this.sortDir = col === 'name' || col === 'sector' ? 'asc' : 'desc';
    }
  }

  toggleAggSort(col: AggSortCol): void {
    if (this.aggSortCol === col) {
      this.aggSortDir = this.aggSortDir === 'asc' ? 'desc' : 'asc';
    } else {
      this.aggSortCol = col;
      this.aggSortDir = col === 'label' ? 'asc' : 'desc';
    }
  }

  get aggregatedRows(): AggregateRow[] {
    const sourceRows = this.filteredChildren;
    if (this.aggregateBy === 'asset') return [];

    const groups = new Map<string, ChildRow[]>();
    for (const row of sourceRows) {
      const key = (this.aggregateBy === 'sector'
        ? row.position.sector
        : row.position.region) ?? '—';
      if (!groups.has(key)) groups.set(key, []);
      groups.get(key)!.push(row);
    }

    const result: AggregateRow[] = [];
    for (const [label, rows] of groups) {
      const withHolding = rows.filter(r => r.holding !== null);
      const avg = (fn: (r: ChildRow) => number) =>
        withHolding.length ? withHolding.reduce((s, r) => s + fn(r), 0) / withHolding.length : 0;

      result.push({
        label,
        count: rows.length,
        iconUrls: rows.map(r => r.position.iconUrl).filter((u): u is string => !!u).slice(0, 4),
        market:         avg(r => r.holding!.marketDelta),
        fundamental:    avg(r => r.holding!.fundamentalDelta),
        publicSentiment: avg(r => r.holding!.publicSentimentDelta),
        memberSentiment: avg(r => r.holding!.memberSentimentDelta),
        instFlow:       avg(r => r.holding!.institutionalOrderFlowDelta),
        pattern:        avg(r => r.holding!.patternDelta),
        score:          avg(r => r.holding!.combinedScore),
        targetFraction: rows.reduce((s, r) => s + (r.holding?.targetFraction ?? 0), 0),
        currentFraction: rows.reduce((s, r) => s + (r.position.fraction ?? 0), 0),
      });
    }

    return result.sort((a, b) => {
      const dir = this.aggSortDir === 'asc' ? 1 : -1;
      let av: string | number;
      let bv: string | number;
      switch (this.aggSortCol) {
        case 'label':       av = a.label;           bv = b.label;           break;
        case 'count':       av = a.count;           bv = b.count;           break;
        case 'market':      av = a.market;          bv = b.market;          break;
        case 'fundamental': av = a.fundamental;     bv = b.fundamental;     break;
        case 'public-sentiment': av = a.publicSentiment; bv = b.publicSentiment; break;
        case 'member-sentiment': av = a.memberSentiment; bv = b.memberSentiment; break;
        case 'inst-flow':   av = a.instFlow;        bv = b.instFlow;        break;
        case 'pattern':     av = a.pattern;         bv = b.pattern;         break;
        case 'score':       av = a.score;           bv = b.score;           break;
        case 'target':      av = a.targetFraction;  bv = b.targetFraction;  break;
        case 'current':     av = a.currentFraction; bv = b.currentFraction; break;
        default:            av = a.score;           bv = b.score;
      }
      if (typeof av === 'string' && typeof bv === 'string') return dir * av.localeCompare(bv);
      return dir * ((av as number) - (bv as number));
    });
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
    this.showOrderHelper = false;
    this.orderMode = 'buy';
    this.orderMoney = 0;
    this.orderSaving = false;
    this.hideZeroDelta = false;
    this.portfolioRemainder = 0;
    this.isStarred = false;
    this.starring = false;
    this.livePrice = null;
    this.liveByAssetId.clear();
    this.sentiment = null;
    this.sentimentLoading = false;

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
            positions: this.positionsService.getPortfolioPositions(assetId),
            holdings: this.assetService.getHoldings(assetId),
            remainder: this.positionsService.getPortfolioRemainder(assetId),
          }).subscribe({
            next: ({ positions, holdings, remainder }) => {
              const holdingMap = new Map(holdings.map((h) => [h.assetId, h]));
              this.children = positions.map((p) => ({
                position: p,
                holding: holdingMap.get(p.assetId) ?? null,
              }));
              this.portfolioRemainder = remainder;
              this.startLivePriceStream(assetId, this.children.map(c => c.position.assetId));
              this.loading = false;
            },
            error: () => { this.loading = false; },
          });
        } else {
          this.startLivePriceStream(assetId, []);
          this.loading = false;
        }

        // Load sentiment independently so it doesn't block the main view
        this.sentimentLoading = true;
        this.assetService.getSentiment(assetId).subscribe({
          next: s => { this.sentiment = s; this.sentimentLoading = false; },
          error: () => { this.sentimentLoading = false; },
        });
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
        positions: this.positionsService.getPortfolioPositions(this.assetId),
        holdings: this.assetService.getHoldings(this.assetId),
      }).subscribe({
        next: ({ delta, positions, holdings }) => {
          this.delta = delta;
          this.latestDelta = delta;

          var holdingMap = new Map(holdings.map((h) => [h.assetId, h]));
          this.children = positions.map((p) => ({
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

  get orderRows(): OrderRow[] {
    const money = Math.max(0, Math.round(this.orderMoney));
    const baseRows = this.children
      .filter(r => r.holding !== null)
      .map(r => {
        const live = this.liveByAssetId.get(this.normalizeAssetId(r.position.assetId));
        const price = live?.price ?? 0;
        const tf = r.holding!.targetFraction;
        const nokAlloc = tf * money;
        const exact = price > 0 ? nokAlloc / price : 0;
        const initial = Math.floor(exact);
        return {
          assetId: r.position.assetId,
          name: r.position.name,
          symbol: r.position.symbol,
          iconUrl: r.position.iconUrl,
          price,
          targetPct: tf * 100,
          initial,
          fractionalPart: exact - initial,
          current: this.orderMode === 'update' ? r.position.quantity : 0,
        };
      });

    // Distribute remaining NOK as +1 share in descending fractional-part order,
    // only if we can still afford the share at its price.
    let remainingNok = money - baseRows.reduce((s, r) => s + r.initial * r.price, 0);
    const ranked = [...baseRows]
      .map((r, i) => ({ i, fp: r.fractionalPart, price: r.price }))
      .sort((a, b) => b.fp - a.fp);
    const residualSet = new Set<number>();
    for (const { i, price } of ranked) {
      if (price > 0 && remainingNok >= price) {
        residualSet.add(i);
        remainingNok -= price;
      }
    }

    return baseRows.map((r, i) => {
      const residual = residualSet.has(i) ? 1 : 0;
      const total = r.initial + residual;
      return {
        assetId: r.assetId,
        name: r.name,
        symbol: r.symbol,
        iconUrl: r.iconUrl,
        price: r.price,
        targetPct: r.targetPct,
        total,
        positionValue: Math.round(total * r.price),
        current: r.current,
        delta: total - r.current,
      };
    });
  }

  get orderTotals(): { invested: number; remainder: number } {
    const rows = this.orderRows;
    const invested = rows.reduce((s, r) => s + r.positionValue, 0);
    const remainder = Math.round(this.orderMoney - rows.reduce((s, r) => s + r.total * r.price, 0));
    return { invested, remainder };
  }

  exportOrderCsv(): void {
    const rows = this.orderRows.filter(r => !this.hideZeroDelta || r.delta !== 0);
    const header = 'Name,Symbol,Price,Target%,Total,Value,Current,Delta';
    const lines = rows.map(r =>
      `"${r.name}","${r.symbol}",${r.price.toFixed(2)},${r.targetPct.toFixed(2)},${r.total},${r.positionValue},${r.current},${r.delta}`
    );
    const csv = [header, ...lines].join('\n');
    const blob = new Blob([csv], { type: 'text/csv' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `order-helper-${this.asset?.name ?? 'portfolio'}.csv`;
    a.click();
    URL.revokeObjectURL(url);
  }

  openOrderHelper(): void {
    this.orderMode = 'update';
    this.showOrderHelper = true;
    this.orderMoney = this.computePortfolioValue();
  }

  switchOrderMode(mode: OrderMode): void {
    this.orderMode = mode;
    this.orderMoney = mode === 'update' ? this.computePortfolioValue() : 0;
  }

  private computePortfolioValue(): number {
    return Math.round(
      this.portfolioRemainder +
      this.children.reduce((sum, r) => {
        const live = this.liveByAssetId.get(this.normalizeAssetId(r.position.assetId));
        return sum + (live ? live.price * r.position.quantity : 0);
      }, 0)
    );
  }

  closeAndUpdateOrder(): void {
    if (!this.assetId || this.orderSaving) return;
    const rows = this.orderRows;
    const updates = rows.map(r => ({ assetId: r.assetId, quantity: r.total }));
    const newRemainder = Math.round(
      this.orderMoney - rows.reduce((s, r) => s + r.total * r.price, 0)
    );
    this.orderSaving = true;
    this.positionsService.updatePortfolioQuantities(this.assetId, updates).subscribe({
      next: () => {
        // Update in-memory quantities so the holdings table and order helper reflect the new state
        const quantityMap = new Map(updates.map(u => [u.assetId, u.quantity]));
        for (const child of this.children) {
          const newQty = quantityMap.get(child.position.assetId);
          if (newQty !== undefined) {
            child.position = { ...child.position, quantity: newQty };
          }
        }
        // Persist the undeployable cash remainder so portfolio value stays stable
        this.portfolioRemainder = newRemainder;
        this.positionsService.setPortfolioRemainder(this.assetId!, newRemainder).subscribe();
        this.showOrderHelper = false;
        this.orderSaving = false;
      },
      error: () => { this.orderSaving = false; },
    });
  }

  ngOnDestroy(): void {
    this.routeSub?.unsubscribe();
    this.liveSub?.unsubscribe();
    this.hoverSub.unsubscribe();
    this.hoverDate$.complete();
  }
}
