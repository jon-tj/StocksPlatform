import { Component, Input, Output, EventEmitter, OnInit, inject } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AssetChip } from '../asset-chip/asset-chip';
import { GainChip } from '../gain-chip/gain-chip';
import { LivePrice } from '../../services/asset.service';
import { PositionsService } from '../../services/positions.service';
import { ChildRow } from '../holdings-table/holdings-table';

export type OrderMode = 'buy' | 'update';

interface OrderRow {
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

@Component({
  selector: 'app-order-helper',
  imports: [RouterLink, DecimalPipe, FormsModule, AssetChip, GainChip],
  templateUrl: './order-helper.html',
  styleUrl: './order-helper.css',
})
export class OrderHelper implements OnInit {
  @Input({ required: true }) children!: ChildRow[];
  @Input({ required: true }) assetId!: string;
  @Input() assetName: string | null = null;
  @Input() liveByAssetId: Map<string, LivePrice> = new Map();
  @Input() portfolioRemainder = 0;
  @Output() closed = new EventEmitter<void>();
  @Output() portfolioRemainderChange = new EventEmitter<number>();

  private positionsService = inject(PositionsService);
  private router = inject(Router);

  orderMode: OrderMode = 'update';
  orderMoney = 0;
  orderSaving = false;
  hideZeroDelta = false;

  ngOnInit(): void {
    this.orderMoney = this.computePortfolioValue();
  }

  switchOrderMode(mode: OrderMode): void {
    this.orderMode = mode;
    this.orderMoney = mode === 'update' ? this.computePortfolioValue() : 0;
  }

  get orderRows(): OrderRow[] {
    const money = Math.max(0, Math.round(this.orderMoney));
    const baseRows = this.children
      .filter(r => r.holding !== null)
      .map(r => {
        const live = this.liveByAssetId.get(this.normalizeId(r.position.assetId));
        const price = live?.price ?? 0;
        const tf = r.holding!.targetFraction;
        const exact = price > 0 ? (tf * money) / price : 0;
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

    let remainingNok = money - baseRows.reduce((s, r) => s + r.initial * r.price, 0);
    const ranked = [...baseRows].map((r, i) => ({ i, fp: r.fractionalPart, price: r.price })).sort((a, b) => b.fp - a.fp);
    const residualSet = new Set<number>();
    for (const { i, price } of ranked) {
      if (price > 0 && remainingNok >= price) { residualSet.add(i); remainingNok -= price; }
    }

    return baseRows.map((r, i) => {
      const total = r.initial + (residualSet.has(i) ? 1 : 0);
      return {
        assetId: r.assetId, name: r.name, symbol: r.symbol, iconUrl: r.iconUrl,
        price: r.price, targetPct: r.targetPct,
        total,
        positionValue: Math.round(total * r.price),
        current: r.current,
        delta: total - r.current,
      };
    });
  }

  get orderTotals(): { invested: number; remainder: number } {
    const rows = this.orderRows;
    return {
      invested: rows.reduce((s, r) => s + r.positionValue, 0),
      remainder: Math.round(this.orderMoney - rows.reduce((s, r) => s + r.total * r.price, 0)),
    };
  }

  exportOrderCsv(): void {
    const rows = this.orderRows.filter(r => !this.hideZeroDelta || r.delta !== 0);
    const header = 'Name,Symbol,Price,Target%,Total,Value,Current,Delta';
    const lines = rows.map(r =>
      `"${r.name}","${r.symbol}",${r.price.toFixed(2)},${r.targetPct.toFixed(2)},${r.total},${r.positionValue},${r.current},${r.delta}`
    );
    const blob = new Blob([[header, ...lines].join('\n')], { type: 'text/csv' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `order-helper-${this.assetName ?? 'portfolio'}.csv`;
    a.click();
    URL.revokeObjectURL(url);
  }

  closeAndUpdateOrder(): void {
    if (this.orderSaving) return;
    const rows = this.orderRows;
    const updates = rows.map(r => ({ assetId: r.assetId, quantity: r.total }));
    const newRemainder = Math.round(this.orderMoney - rows.reduce((s, r) => s + r.total * r.price, 0));
    this.orderSaving = true;
    this.positionsService.updatePortfolioQuantities(this.assetId, updates).subscribe({
      next: () => {
        const quantityMap = new Map(updates.map(u => [u.assetId, u.quantity]));
        for (const child of this.children) {
          const newQty = quantityMap.get(child.position.assetId);
          if (newQty !== undefined) child.position = { ...child.position, quantity: newQty };
        }
        this.portfolioRemainderChange.emit(newRemainder);
        this.positionsService.setPortfolioRemainder(this.assetId, newRemainder).subscribe();
        this.closed.emit();
        this.orderSaving = false;
      },
      error: () => { this.orderSaving = false; },
    });
  }

  openChildAnalysis(assetId: string): void {
    this.router.navigate(['/analysis', assetId]);
  }

  private computePortfolioValue(): number {
    return Math.round(
      this.portfolioRemainder +
      this.children.reduce((sum, r) => {
        const live = this.liveByAssetId.get(this.normalizeId(r.position.assetId));
        return sum + (live ? live.price * r.position.quantity : 0);
      }, 0)
    );
  }

  private normalizeId(id: string): string { return id.trim().toLowerCase(); }
}
