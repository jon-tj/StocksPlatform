import { ChangeDetectorRef, Component, ElementRef, inject, Input, OnDestroy, AfterViewInit, ViewChild } from '@angular/core';
import { DecimalPipe } from '@angular/common';

export interface ReturnsSeries {
  name?: string;
  returns: number[];
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
  @Input({ required: true }) returnsSeries: ReturnsSeries[] = [];
  @Input() title = '';

  @ViewChild('svgWrap') svgWrapRef!: ElementRef<HTMLElement>;

  W = 500;
  readonly H = 140;
  readonly padY = 14;

  hoveredIndex: number | null = null;
  hoverX = 0;
  mode: 'cumulative' | 'period' = 'cumulative';

  private ro!: ResizeObserver;
  private cdr = inject(ChangeDetectorRef);

  ngAfterViewInit(): void {
    this.ro = new ResizeObserver((entries) => {
      this.W = entries[0].contentRect.width;
      this.cdr.markForCheck();
    });
    this.ro.observe(this.svgWrapRef.nativeElement);
    this.W = this.svgWrapRef.nativeElement.clientWidth;
  }

  ngOnDestroy(): void {
    this.ro?.disconnect();
  }

  getColor(i: number): string {
    return COLORS[i % COLORS.length];
  }

  get timeLabels(): string[] {
    return this.returnsSeries[0]?.times ?? [];
  }

  get timeFrom(): string {
    return this.timeLabels[0] ?? '';
  }

  get timeTo(): string {
    const t = this.timeLabels;
    if (!t.length) return '';
    return t[t.length - 1];
  }

  /** Label shown next to the title — single period when hovering a bar, full range otherwise. */
  get displayedTimeRange(): string {
    const t = this.timeLabels;
    if (!t.length) return '';
    if (this.mode === 'period' && this.hoveredIndex !== null) {
      return t[this.hoveredIndex];
    }
    return t.length === 1 ? t[0] : `${this.timeFrom} \u2013 ${this.timeTo}`;
  }

  cumulativeReturns(s: ReturnsSeries): number[] {
    let sum = 0;
    return s.returns.map((v) => +(sum += v).toFixed(2));
  }

  /** Return value shown in legend: cumulative up to idx, or period at idx. */
  getLiveValue(s: ReturnsSeries): number {
    const idx = this.hoveredIndex ?? s.returns.length - 1;
    if (this.mode === 'period') {
      return +s.returns[idx].toFixed(1);
    }
    return +(s.returns.slice(0, idx + 1).reduce((a, b) => a + b, 0)).toFixed(1);
  }

  private get allValues(): number[] {
    if (this.mode === 'period') {
      return this.returnsSeries.flatMap((s) => s.returns);
    }
    return this.returnsSeries.flatMap((s) => this.cumulativeReturns(s));
  }

  private get yMin(): number {
    return Math.min(0, ...this.allValues);
  }

  private get yMax(): number {
    return Math.max(0, ...this.allValues);
  }

  private toY(v: number): number {
    const range = this.yMax - this.yMin || 1;
    return +(this.padY + (1 - (v - this.yMin) / range) * (this.H - this.padY * 2)).toFixed(1);
  }

  private toX(i: number, len: number): number {
    return +((i / (len - 1)) * this.W).toFixed(1);
  }

  getLinePath(s: ReturnsSeries): string {
    const cum = this.cumulativeReturns(s);
    const pts = cum.map((v, i) => `${this.toX(i, cum.length)},${this.toY(v)}`);
    return `M ${pts.join(' L ')}`;
  }

  getAreaPath(s: ReturnsSeries): string {
    return `${this.getLinePath(s)} L ${this.W},${this.H} L 0,${this.H} Z`;
  }

  getPointY(s: ReturnsSeries, idx: number): number {
    return this.toY(this.cumulativeReturns(s)[idx]);
  }

  gradId(i: number): string {
    return `sc-grad-${i}-${this.title.replace(/\s/g, '') || 'chart'}`;
  }

  getZeroY(): number {
    return this.toY(0);
  }

  getBarRects(s: ReturnsSeries, si: number, n: number): { x: number; y: number; w: number; h: number }[] {
    const len = s.returns.length;
    const groupW = this.W / len;
    const barW = Math.max(1, (groupW * 0.72) / n);
    const groupPad = (groupW - barW * n) / 2;
    const zero = this.toY(0);
    return s.returns.map((v, i) => {
      const x = +(i * groupW + groupPad + si * barW).toFixed(1);
      const yV = this.toY(v);
      return {
        x,
        y: +Math.min(yV, zero).toFixed(1),
        w: +barW.toFixed(1),
        h: +Math.max(1, Math.abs(zero - yV)).toFixed(1),
      };
    });
  }

  onModeChange(e: Event): void {
    this.mode = (e.target as HTMLSelectElement).value as 'cumulative' | 'period';
    this.hoveredIndex = null;
  }

  onMouseMove(e: MouseEvent): void {
    const rect = (e.currentTarget as HTMLElement).getBoundingClientRect();
    const relX = e.clientX - rect.left;
    const len = this.timeLabels.length;
    if (!len) return;
    const idx = Math.round((relX / rect.width) * (len - 1));
    this.hoveredIndex = Math.max(0, Math.min(len - 1, idx));
    if (this.mode === 'period') {
      this.hoverX = +((idx + 0.5) * (this.W / len)).toFixed(1);
    } else {
      this.hoverX = +((this.hoveredIndex / (len - 1)) * this.W).toFixed(1);
    }
  }

  onMouseLeave(): void {
    this.hoveredIndex = null;
  }
}

