import { Component, Input } from '@angular/core';
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
export class StockChart {
  @Input({ required: true }) returnsSeries: ReturnsSeries[] = [];
  @Input() title = '';

  readonly W = 500;
  readonly H = 140;
  readonly padY = 14;

  hoveredIndex: number | null = null;
  hoverX = 0;
  tooltipLeft = 0;

  getColor(i: number): string {
    return COLORS[i % COLORS.length];
  }

  get hasLegend(): boolean {
    return this.returnsSeries.length > 1 || this.returnsSeries.some((s) => s.name);
  }

  get totalReturn(): number {
    const all = this.returnsSeries[0]?.returns ?? [];
    return +all.reduce((s, v) => s + v, 0).toFixed(1);
  }

  get timeLabels(): string[] {
    return this.returnsSeries[0]?.times ?? [];
  }

  private get allValues(): number[] {
    return this.returnsSeries.flatMap((s) => s.returns);
  }

  private get yMin(): number {
    return Math.min(...this.allValues);
  }

  private get yMax(): number {
    return Math.max(...this.allValues);
  }

  private toY(v: number): number {
    const range = this.yMax - this.yMin || 1;
    return +(this.padY + (1 - (v - this.yMin) / range) * (this.H - this.padY * 2)).toFixed(1);
  }

  private toX(i: number, len: number): number {
    return +((i / (len - 1)) * this.W).toFixed(1);
  }

  getLinePath(s: ReturnsSeries): string {
    const pts = s.returns.map((v, i) => `${this.toX(i, s.returns.length)},${this.toY(v)}`);
    return `M ${pts.join(' L ')}`;
  }

  getAreaPath(s: ReturnsSeries): string {
    return `${this.getLinePath(s)} L ${this.W},${this.H} L 0,${this.H} Z`;
  }

  getPointY(s: ReturnsSeries, idx: number): number {
    return this.toY(s.returns[idx]);
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
    const tooltipW = 150;
    this.tooltipLeft = relX + tooltipW > rect.width ? relX - tooltipW - 8 : relX + 12;
  }

  onMouseLeave(): void {
    this.hoveredIndex = null;
  }
}

