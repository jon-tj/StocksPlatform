import {
  Component, OnDestroy,
  ElementRef, viewChild, input, signal, computed, effect, inject,
} from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { Subscription } from 'rxjs';
import { AssetService, OrderBookSnapshotDto, OrderBookLevel } from '../../services/asset.service';

interface ChartItem {
  cx: number;
  cy: number;
  tailX2: number;
  r: number;
  tailOpacity: number;
  color: string;
  tooltip: string;
}

interface YTick { y: number; label: string; }
interface XTick { ms: number; x: number; xPct: number; label: string; }

@Component({
  selector: 'app-order-chart',
  standalone: true,
  imports: [DecimalPipe],
  templateUrl: './order-chart.html',
  styleUrl: './order-chart.css',
})
export class OrderChart implements OnDestroy {
  private assetService = inject(AssetService);

  assetId = input.required<string>();
  hours   = input<number>(72);

  readonly H        = 300;
  readonly padLeft  = 10;
  readonly padRight = 60;
  readonly padY     = 20;

  orderBook = input<OrderBookLevel[]>([]);

  snapshots = signal<OrderBookSnapshotDto[]>([]);
  loading   = signal(false);

  readonly sizeMin  = 0.25;
  readonly sizeMax  = 3.0;
  readonly sizeStep = 0.25;
  sizeScale = signal(1.0);

  adjustSize(delta: number) {
    const next = Math.round((this.sizeScale() + delta) / this.sizeStep) * this.sizeStep;
    this.sizeScale.set(Math.min(this.sizeMax, Math.max(this.sizeMin, next)));
  }

  W = signal(500);

  private chartColRef = viewChild<ElementRef<HTMLElement>>('ocChart');
  private resizeObs: ResizeObserver | null = null;
  private sub: Subscription | null = null;

  constructor() {
    try {
      const v = parseFloat(localStorage.getItem('oc-sizeScale') ?? '');
      if (Number.isFinite(v)) this.sizeScale.set(Math.min(this.sizeMax, Math.max(this.sizeMin, v)));
    } catch { /* ignore */ }
    effect(() => this.load(this.assetId(), this.hours()));
    effect(() => { localStorage.setItem('oc-sizeScale', String(this.sizeScale())); });
    effect(() => {
      const el = this.chartColRef()?.nativeElement;
      this.resizeObs?.disconnect();
      this.resizeObs = null;
      if (!el) return;
      const obs = new ResizeObserver(entries => {
        const w = entries[0]?.contentRect.width;
        if (w > 0) this.W.set(Math.round(w));
      });
      obs.observe(el);
      this.resizeObs = obs;
    });
  }

  ngOnDestroy() {
    this.resizeObs?.disconnect();
    this.sub?.unsubscribe();
  }

  private load(assetId: string, hours: number) {
    this.sub?.unsubscribe();
    const from = new Date(Date.now() - hours * 3_600_000).toISOString();
    this.loading.set(true);
    this.sub = this.assetService.getOrderBookHistory(assetId, { from }).subscribe({
      next: data => { this.snapshots.set(data); this.loading.set(false); },
      error: ()   => this.loading.set(false),
    });
  }

  // ── Derived chart geometry ──────────────────────────────────────────────────

  private bounds = computed(() => {
    const snaps = this.snapshots();
    if (!snaps.length) return null;
    const now    = Date.now();
    const times  = snaps.map(s => new Date(s.timestamp).getTime());
    const prices = snaps.map(s => s.price).filter(p => p > 0);
    if (!prices.length) return null;

    const absIncs = snaps.map(s => Math.max(Math.abs(s.increment), 1));
    const absVols = snaps.map(s => Math.max(Math.abs(s.newVol),    1));

    return {
      timeFrom:  Math.min(...times),
      timeTo:    now,
      priceMin:  Math.min(...prices),
      priceMax:  Math.max(...prices),
      logIncMin: Math.log(Math.min(...absIncs)),
      logIncMax: Math.log(Math.max(...absIncs)),
      logVolMin: Math.log(Math.min(...absVols)),
      logVolMax: Math.log(Math.max(...absVols)),
    };
  });

  hasData = computed(() => !!this.bounds());

  chartItems = computed((): ChartItem[] => {
    const b = this.bounds();
    if (!b) return [];

    const { timeFrom, timeTo, priceMin, priceMax, logIncMin, logIncMax, logVolMin, logVolMax } = b;
    const plotW   = this.W() - this.padLeft - this.padRight;
    const plotH   = this.H  - 2 * this.padY;
    const tRange  = Math.max(timeTo - timeFrom, 1);
    const pRange  = Math.max(priceMax - priceMin, 1);
    const incRange = logIncMax - logIncMin || 1;
    const volRange = logVolMax - logVolMin || 1;
    const nowX    = this.W() - this.padRight;

    return this.snapshots().map(s => {
      const t        = new Date(s.timestamp).getTime();
      const cx       = this.padLeft + ((t - timeFrom) / tRange) * plotW;
      const cy       = this.padY + (1 - (s.price - priceMin) / pRange) * plotH;
      const normInc  = (Math.log(Math.max(Math.abs(s.increment), 1)) - logIncMin) / incRange;
      const normVol  = (Math.log(Math.max(Math.abs(s.newVol),    1)) - logVolMin) / volRange;

      // radius 5–25 px → diameter 10–50 px, scaled by user preference
      const r            = (5 + Math.min(1, Math.max(0, normInc)) * 20) * this.sizeScale();
      const tailOpacity  = 0.1 + Math.min(1, Math.max(0, normVol)) * 0.9;
      const color        = s.side === 'Bid' ? '#4ade80' : '#f87171';
      const incSign      = s.increment >= 0 ? '+' : '';
      const tooltip      = `${s.side} L${s.level} · ${s.price.toFixed(2)} · vol ${Math.round(s.newVol)} · Δ${incSign}${Math.round(s.increment)}`;

      return { cx, cy, tailX2: nowX, r, tailOpacity, color, tooltip };
    });
  });

  yTicks = computed((): YTick[] => {
    const b = this.bounds();
    if (!b) return [];
    const { priceMin, priceMax } = b;
    const plotH = this.H - 2 * this.padY;
    const range = priceMax - priceMin;
    if (range === 0) return [{ y: this.H / 2, label: priceMin.toFixed(1) }];
    return Array.from({ length: 5 }, (_, i) => {
      const t = i / 4;
      return { y: this.padY + (1 - t) * plotH, label: (priceMin + t * range).toFixed(1) };
    });
  });

  xTicks = computed((): XTick[] => {
    const b = this.bounds();
    if (!b) return [];
    const { timeFrom, timeTo } = b;
    const plotW  = this.W() - this.padLeft - this.padRight;
    const range  = timeTo - timeFrom;
    if (range <= 0) return [];

    const hourMs    = 3_600_000;
    const stepHours = Math.max(1, Math.round(range / 6 / hourMs));
    const stepMs    = stepHours * hourMs;
    const ticks: XTick[] = [];

    for (let t = Math.ceil(timeFrom / stepMs) * stepMs; t < timeTo; t += stepMs) {
      const x    = this.padLeft + ((t - timeFrom) / range) * plotW;
      const xPct = (x / this.W()) * 100;
      const d    = new Date(t);
      const label = `${String(d.getDate()).padStart(2, '0')}/${String(d.getMonth() + 1).padStart(2, '0')} ${String(d.getHours()).padStart(2, '0')}:00`;
      ticks.push({ ms: t, x, xPct, label });
    }
    return ticks;
  });
}
