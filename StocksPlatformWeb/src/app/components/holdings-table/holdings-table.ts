import { Component, Input, Output, EventEmitter, inject } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { SectorLabelPipe } from '../../pipes/sector-label.pipe';
import { AssetChip } from '../asset-chip/asset-chip';
import { GainChip } from '../gain-chip/gain-chip';
import { OrderHelper } from '../order-helper/order-helper';
import { LivePrice, HoldingDelta } from '../../services/asset.service';
import { Position } from '../../services/positions.service';

export interface ChildRow {
  position: Position;
  holding: HoldingDelta | null;
}

interface AggregateRow {
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

type SortCol =
  | 'name' | 'sector'
  | 'market' | 'fundamental' | 'public-sentiment' | 'member-sentiment'
  | 'inst-flow' | 'pattern' | 'score' | 'target' | 'current';

type AggSortCol = 'label' | 'count' | 'market' | 'fundamental' | 'public-sentiment' | 'member-sentiment' | 'inst-flow' | 'pattern' | 'score' | 'target' | 'current';

type AggregateBy = 'asset' | 'sector' | 'region';

@Component({
  selector: 'app-holdings-table',
  imports: [RouterLink, DecimalPipe, FormsModule, SectorLabelPipe, AssetChip, GainChip, OrderHelper],
  templateUrl: './holdings-table.html',
  styleUrl: './holdings-table.css',
})
export class HoldingsTable {
  @Input({ required: true }) children!: ChildRow[];
  @Input({ required: true }) assetId!: string;
  @Input() assetName: string | null = null;
  @Input() liveByAssetId: Map<string, LivePrice> = new Map();
  @Input() portfolioRemainder = 0;
  @Output() portfolioRemainderChange = new EventEmitter<number>();

  private router = inject(Router);

  holdingsFilter = '';
  sortCol: SortCol | null = 'score';
  sortDir: 'asc' | 'desc' = 'desc';
  aggregateBy: AggregateBy = 'asset';
  aggSortCol: AggSortCol = 'score';
  aggSortDir: 'asc' | 'desc' = 'desc';
  showOrderHelper = false;

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
      if (av == null) return 1;
      if (bv == null) return -1;
      if (typeof av === 'string' && typeof bv === 'string') return dir * av.localeCompare(bv);
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
        market:          avg(r => r.holding!.marketDelta),
        fundamental:     avg(r => r.holding!.fundamentalDelta),
        publicSentiment: avg(r => r.holding!.publicSentimentDelta),
        memberSentiment: avg(r => r.holding!.memberSentimentDelta),
        instFlow:        avg(r => r.holding!.institutionalOrderFlowDelta),
        pattern:         avg(r => r.holding!.patternDelta),
        score:           avg(r => r.holding!.combinedScore),
        targetFraction:  rows.reduce((s, r) => s + (r.holding?.targetFraction ?? 0), 0),
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

  openChildAnalysis(assetId: string): void {
    this.router.navigate(['/analysis', assetId]);
  }
}
