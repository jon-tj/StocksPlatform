import { Component, ElementRef, OnDestroy, ViewChild, computed, input, output, signal } from '@angular/core';
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
export class StockChart implements OnDestroy {
  priceSeries = input.required<PriceSeries[]>();
  title = input<string>('');
  dateHover = output<string | null>();

  readonly hasChartData = computed(() =>
    this.priceSeries().some((series) => {
      const pointCount = Math.min(series.prices.length, series.times.length);
      if (pointCount === 0) return false;
      for (let i = 0; i < pointCount; i++) {
        if (Number.isFinite(series.prices[i])) {
          return true;
        }
      }
      return false;
    })
  );

  @ViewChild('svgWrap')
  set svgWrapRef(ref: ElementRef<HTMLElement> | undefined) {
    this.ro?.disconnect();
    if (!ref) return;

    this.ro = new ResizeObserver((entries) => {
      this.W.set(entries[0].contentRect.width);
    });
    this.ro.observe(ref.nativeElement);
    this.W.set(ref.nativeElement.clientWidth);
  }

  W = signal(500);
  readonly H = 200;
  readonly padLeft = 6;
  readonly padRight = 76;
  readonly padY = 14;

  hoveredIndex: number | null = null;
  hoverX = 0;
  mode = signal<'price' | 'returns'>('price');

  private ro: ResizeObserver | null = null;

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
    const width = this.plotWidth();
    const ticks: { label: string; pct: number }[] = [];
    let lastMonth = '';
    for (let i = 0; i < times.length; i++) {
      const month = times[i].slice(0, 3);
      if (month !== lastMonth) {
        lastMonth = month;
        const x = this.padLeft + (times.length === 1 ? 0 : (i / (times.length - 1)) * width);
        ticks.push({ label: month, pct: +((x / this.W()) * 100).toFixed(2) });
      }
    }
    return ticks;
  });

  private readonly allValues = computed(() => {
    if (this.mode() === 'returns') {
      return this.periodReturnsCache().flat();
    }
    return this.priceSeries().flatMap(s => s.prices);
  });

  private readonly yMin = computed(() => {
    const values = this.allValues();
    if (!values.length) return 0;

    const min = Math.min(...values);
    const max = Math.max(...values);
    if (this.mode() === 'returns') {
      return Math.min(0, min);
    }

    const range = max - min;
    const padding = range > 0 ? range * 0.08 : Math.max(Math.abs(min) * 0.04, 1);
    return min - padding;
  });

  private readonly yMax = computed(() => {
    const values = this.allValues();
    if (!values.length) return 1;

    const min = Math.min(...values);
    const max = Math.max(...values);
    if (this.mode() === 'returns') {
      return Math.max(0, max);
    }

    const range = max - min;
    const padding = range > 0 ? range * 0.08 : Math.max(Math.abs(max) * 0.04, 1);
    return max + padding;
  });
  readonly rightAxisTicks = computed(() => {
    if (this.mode() !== 'price') return [];

    const min = this.yMin();
    const max = this.yMax();
    if (!Number.isFinite(min) || !Number.isFinite(max)) return [];
    if (min === max) {
      return [{ y: this.toY(min), label: this.formatPrice(min) }];
    }

    const count = 4;
    return Array.from({ length: count + 1 }, (_, index) => {
      const value = max - ((max - min) * index) / count;
      return {
        y: this.toY(value),
        label: this.formatPrice(value),
      };
    });
  });
  private readonly priceLabelDigits = computed(() => {
    const range = this.yMax() - this.yMin();
    if (range >= 1000) return 0;
    if (range >= 20) return 1;
    return 2;
  });

  ngOnDestroy(): void {
    this.ro?.disconnect();
    this.ro = null;
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
    if (this.mode() === 'returns' && this.hoveredIndex !== null) {
      return t[this.hoveredIndex];
    }
    const end = t[this.hoveredIndex ?? t.length - 1];
    return t.length === 1 ? t[0] : `${this.timeFrom} \u2013 ${end}`;
  }

  /** Total return in price mode, or bar return in returns mode. */
  getLiveValue(s: PriceSeries): number {
    const idx = this.hoveredIndex ?? s.prices.length - 1;
    if (this.mode() === 'returns') {
      const si = this.priceSeries().indexOf(s);
      const period = si >= 0 ? this.periodReturnsCache()[si] : [];
      return +(period[idx] ?? 0).toFixed(2);
    }

    const base = s.prices[0];
    const current = s.prices[idx];
    if (!base || current == null) return 0;
    return +((current / base - 1) * 100).toFixed(2);
  }

  /** Actual price in price mode, or price change from previous bar in returns mode. */
  getLivePrice(s: PriceSeries): number {
    const idx = this.hoveredIndex ?? s.prices.length - 1;
    if (this.mode() === 'returns') {
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
    if (len <= 1) return this.padLeft;
    return +(this.padLeft + (i / (len - 1)) * this.plotWidth()).toFixed(1);
  }

  getLinePath(s: PriceSeries): string {
    const pts = s.prices.map((v, i) => `${this.toX(i, s.prices.length)},${this.toY(v)}`);
    return `M ${pts.join(' L ')}`;
  }

  getAreaPath(s: PriceSeries): string {
    return `${this.getLinePath(s)} L ${this.plotRight()},${this.H - this.padY} L ${this.padLeft},${this.H - this.padY} Z`;
  }

  getPointY(s: PriceSeries, idx: number): number {
    return this.toY(s.prices[idx] ?? 0);
  }

  getLastPointX(s: PriceSeries): number {
    return this.toX(Math.max(0, s.prices.length - 1), s.prices.length);
  }

  getLastPriceLabel(s: PriceSeries): string {
    return this.formatPrice(s.prices[s.prices.length - 1] ?? 0);
  }

  getLastPriceCalloutPoints(s: PriceSeries): string {
    const x = this.getLastPointX(s);
    const y = this.getPointY(s, Math.max(0, s.prices.length - 1));
    const label = this.getLastPriceLabel(s);
    const boxRight = this.W() - 4;
    const boxLeft = Math.max(x + 4, boxRight - (label.length * 8 + 18));
    const boxTop = Math.max(this.getPlotTop() - 2, y - 13);
    const boxBottom = Math.min(this.getPlotBottom() + 2, y + 13);
    return `${x},${y} ${boxLeft},${boxTop} ${boxRight},${boxTop} ${boxRight},${boxBottom} ${boxLeft},${boxBottom}`;
  }

  getLastPriceLabelX(s: PriceSeries): number {
    const boxRight = this.W() - 4;
    return +(boxRight - 8).toFixed(1);
  }

  getPlotBottom(): number {
    return this.H - this.padY;
  }

  getPlotTop(): number {
    return this.padY;
  }

  plotRight(): number {
    return Math.max(this.padLeft, this.W() - this.padRight);
  }

  plotWidth(): number {
    return Math.max(1, this.plotRight() - this.padLeft);
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
    const groupW = this.plotWidth() / Math.max(1, len);
    const barW = Math.max(1, (groupW * 0.72) / n);
    const groupPad = (groupW - barW * n) / 2;
    const zero = this.toY(0);
    return returns.map((v, i) => {
      const x = +(this.padLeft + i * groupW + groupPad + si * barW).toFixed(1);
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
    this.mode.set((e.target as HTMLSelectElement).value as 'price' | 'returns');
    this.hoveredIndex = null;
  }

  onMouseMove(e: MouseEvent): void {
    const rect = (e.currentTarget as HTMLElement).getBoundingClientRect();
    const relX = e.clientX - rect.left;
    const len = this.timeLabels.length;
    if (!len) return;
    const clampedX = Math.max(this.padLeft, Math.min(this.plotRight(), relX));
    const idx = Math.round(((clampedX - this.padLeft) / this.plotWidth()) * (len - 1));
    this.hoveredIndex = Math.max(0, Math.min(len - 1, idx));
    if (this.mode() === 'returns') {
      this.hoverX = +(this.padLeft + (idx + 0.5) * (this.plotWidth() / len)).toFixed(1);
    } else {
      this.hoverX = this.toX(this.hoveredIndex, len);
    }
    this.dateHover.emit(this.timeLabels[this.hoveredIndex] ?? null);
  }

  onMouseLeave(): void {
    this.hoveredIndex = null;
    this.dateHover.emit(null);
  }

  private formatPrice(value: number): string {
    const digits = this.priceLabelDigits();
    return value.toLocaleString(undefined, {
      minimumFractionDigits: digits,
      maximumFractionDigits: digits,
    });
  }
}

