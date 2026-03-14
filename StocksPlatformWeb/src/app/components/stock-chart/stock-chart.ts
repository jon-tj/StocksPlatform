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
    return t[this.hoveredIndex ?? t.length - 1];
  }

  cumulativeReturns(s: ReturnsSeries): number[] {
    let sum = 0;
    return s.returns.map((v) => +(sum += v).toFixed(2));
  }

  /** Cumulative return up to hovered index, or total when not hovering. */
  getLiveValue(s: ReturnsSeries): number {
    const idx = this.hoveredIndex ?? s.returns.length - 1;
    return +(s.returns.slice(0, idx + 1).reduce((a, b) => a + b, 0)).toFixed(1);
  }

  private get allValues(): number[] {
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

  onMouseMove(e: MouseEvent): void {
    const rect = (e.currentTarget as HTMLElement).getBoundingClientRect();
    const relX = e.clientX - rect.left;
    const len = this.timeLabels.length;
    if (!len) return;
    const idx = Math.round((relX / rect.width) * (len - 1));
    this.hoveredIndex = Math.max(0, Math.min(len - 1, idx));
    this.hoverX = +((this.hoveredIndex / (len - 1)) * this.W).toFixed(1);
  }

  onMouseLeave(): void {
    this.hoveredIndex = null;
  }
}

