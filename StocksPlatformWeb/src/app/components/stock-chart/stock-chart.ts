import { Component, ElementRef, AfterViewInit, OnDestroy, ViewChild, computed, input, output, signal } from '@angular/core';
import { DecimalPipe } from '@angular/common';

export interface PriceSeries {
  name?: string;
  prices: number[];
  times: string[];
}

const COLORS = ['#6366f1', '#10b981', '#f59e0b', '#ef4444', '#818cf8'];

@Component({
  selector: 'app-stock-chart',
  imports: [DecimalPipe],
  templateUrl: './stock-chart.html',
  styleUrl: './stock-chart.css',
})
export class StockChart implements AfterViewInit, OnDestroy {
  priceSeries = input.required<PriceSeries[]>();
  title = input<string>('');
  dateHover = output<string | null>();

  @ViewChild('svgWrap') svgWrapRef!: ElementRef<HTMLElement>;

  W = signal(500);
  readonly H = 140;
  readonly padY = 14;

  hoveredIndex: number | null = null;
  hoverX = 0;
  mode = signal<'cumulative' | 'period'>('cumulative');

  private ro!: ResizeObserver;

  /**
   * Cumulative % return from the first price: (price[i] / price[0] - 1) * 100.
   * Used for the cumulative line/area chart.
   */
  readonly cumulativeCache = computed(() =>
    this.priceSeries().map(s => {
      const base = s.prices[0];
      if (!base) return s.prices.map(() => 0);
      return s.prices.map(p => +((p / base - 1) * 100).toFixed(2));
    })
  );

  /**
   * Period (daily) % return: (price[i] / price[i-1] - 1) * 100.
   * First bar is 0. Used for the bar chart.
   */
  readonly periodReturnsCache = computed(() =>
    this.priceSeries().map(s =>
      s.prices.map((p, i) =>
        i === 0 ? 0 : +((p / s.prices[i - 1] - 1) * 100).toFixed(2)
      )
    )
  );

  /** One tick per month: label = "Jan", pct = left offset as 0–100. */
  readonly monthTicks = computed(() => {
    const times = this.priceSeries()[0]?.times ?? [];
    if (!times.length) return [];
    const ticks: { label: string; pct: number }[] = [];
    let lastMonth = '';
    for (let i = 0; i < times.length; i++) {
      const month = times[i].slice(0, 3);
      if (month !== lastMonth) {
        lastMonth = month;
        ticks.push({ label: month, pct: +(i / (times.length - 1) * 100).toFixed(2) });
      }
    }
    return ticks;
  });

  private readonly allValues = computed(() => {
    if (this.mode() === 'period') {
      return this.periodReturnsCache().flat();
    }
    return this.cumulativeCache().flat();
  });

  private readonly yMin = computed(() => Math.min(0, ...this.allValues()));
  private readonly yMax = computed(() => Math.max(0, ...this.allValues()));

  ngAfterViewInit(): void {
    this.ro = new ResizeObserver((entries) => {
      this.W.set(entries[0].contentRect.width);
    });
    this.ro.observe(this.svgWrapRef.nativeElement);
    this.W.set(this.svgWrapRef.nativeElement.clientWidth);
  }

  ngOnDestroy(): void {
    this.ro?.disconnect();
  }

  getColor(i: number): string {
    return COLORS[i % COLORS.length];
  }

  get timeLabels(): string[] {
    return this.priceSeries()[0]?.times ?? [];
  }

  get timeFrom(): string {
    return this.timeLabels[0] ?? '';
  }

  get timeTo(): string {
    const t = this.timeLabels;
    if (!t.length) return '';
    return t[t.length - 1];
  }

  get displayedTimeRange(): string {
    const t = this.timeLabels;
    if (!t.length) return '';
    if (this.mode() === 'period' && this.hoveredIndex !== null) {
      return t[this.hoveredIndex];
    }
    const end = t[this.hoveredIndex ?? t.length - 1];
    return t.length === 1 ? t[0] : `${this.timeFrom} \u2013 ${end}`;
  }

  /** % return to show in the legend (cumulative from start, or daily for period mode). */
  getLiveValue(s: PriceSeries): number {
    const si = this.priceSeries().indexOf(s);
    const idx = this.hoveredIndex ?? s.prices.length - 1;
    if (this.mode() === 'period') {
      const period = si >= 0 ? this.periodReturnsCache()[si] : [];
      return +(period[idx] ?? 0).toFixed(2);
    }
    const cum = si >= 0 ? this.cumulativeCache()[si] : [];
    return +(cum[idx] ?? 0).toFixed(2);
  }

  /** The actual price at the hovered (or last) index. */
  /** Raw price (cumulative mode) or price change from previous bar (period mode). */
  getLivePrice(s: PriceSeries): number {
    const idx = this.hoveredIndex ?? s.prices.length - 1;
    if (this.mode() === 'period') {
      const prev = s.prices[idx - 1];
      const curr = s.prices[idx];
      return prev != null && curr != null ? +(curr - prev).toFixed(2) : 0;
    }
    return s.prices[idx] ?? 0;
  }

  private toY(v: number): number {
    const range = this.yMax() - this.yMin() || 1;
    return +(this.padY + (1 - (v - this.yMin()) / range) * (this.H - this.padY * 2)).toFixed(1);
  }

  private toX(i: number, len: number): number {
    return +((i / (len - 1)) * this.W()).toFixed(1);
  }

  getLinePath(s: PriceSeries): string {
    const si = this.priceSeries().indexOf(s);
    const cum = si >= 0 ? this.cumulativeCache()[si] : [];
    const pts = cum.map((v, i) => `${this.toX(i, cum.length)},${this.toY(v)}`);
    return `M ${pts.join(' L ')}`;
  }

  getAreaPath(s: PriceSeries): string {
    return `${this.getLinePath(s)} L ${this.W()},${this.H} L 0,${this.H} Z`;
  }

  getPointY(s: PriceSeries, idx: number): number {
    const si = this.priceSeries().indexOf(s);
    const cum = si >= 0 ? this.cumulativeCache()[si] : [];
    return this.toY(cum[idx] ?? 0);
  }

  gradId(i: number): string {
    return `sc-grad-${i}-${this.title().replace(/\s/g, '') || 'chart'}`;
  }

  getZeroY(): number {
    return this.toY(0);
  }

  getBarRects(s: PriceSeries, si: number, n: number): { x: number; y: number; w: number; h: number; ret: number }[] {
    const idx = this.priceSeries().indexOf(s);
    const returns = idx >= 0 ? this.periodReturnsCache()[idx] : [];
    const len = returns.length;
    const groupW = this.W() / len;
    const barW = Math.max(1, (groupW * 0.72) / n);
    const groupPad = (groupW - barW * n) / 2;
    const zero = this.toY(0);
    return returns.map((v, i) => {
      const x = +(i * groupW + groupPad + si * barW).toFixed(1);
      const yV = this.toY(v);
      return {
        x,
        y: +Math.min(yV, zero).toFixed(1),
        w: +barW.toFixed(1),
        h: +Math.max(1, Math.abs(zero - yV)).toFixed(1),
        ret: v,
      };
    });
  }

  onModeChange(e: Event): void {
    this.mode.set((e.target as HTMLSelectElement).value as 'cumulative' | 'period');
    this.hoveredIndex = null;
  }

  onMouseMove(e: MouseEvent): void {
    const rect = (e.currentTarget as HTMLElement).getBoundingClientRect();
    const relX = e.clientX - rect.left;
    const len = this.timeLabels.length;
    if (!len) return;
    const idx = Math.round((relX / rect.width) * (len - 1));
    this.hoveredIndex = Math.max(0, Math.min(len - 1, idx));
    if (this.mode() === 'period') {
      this.hoverX = +((idx + 0.5) * (this.W() / len)).toFixed(1);
    } else {
      this.hoverX = +((this.hoveredIndex / (len - 1)) * this.W()).toFixed(1);
    }
    this.dateHover.emit(this.timeLabels[this.hoveredIndex] ?? null);
  }

  onMouseLeave(): void {
    this.hoveredIndex = null;
    this.dateHover.emit(null);
  }
}

