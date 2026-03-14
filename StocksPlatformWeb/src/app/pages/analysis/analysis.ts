import { Component, OnInit, inject } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';
import { DecimalPipe, TitleCasePipe, DatePipe } from '@angular/common';
import { AssetService, AssetDetails, AssetDelta, HoldingDelta } from '../../services/asset.service';
import { PositionsService, Position } from '../../services/positions.service';
import { StockChart, PriceSeries } from '../../components/stock-chart/stock-chart';

export interface ChildRow {
  position: Position;
  holding: HoldingDelta | null;
}

@Component({
  selector: 'app-analysis',
  imports: [RouterLink, DecimalPipe, TitleCasePipe, DatePipe, StockChart],
  templateUrl: './analysis.html',
  styleUrl: './analysis.css',
})
export class Analysis implements OnInit {
  private route = inject(ActivatedRoute);
  private assetService = inject(AssetService);
  private positionsService = inject(PositionsService);

  assetId: string | null = null;
  asset: AssetDetails | null = null;
  delta: AssetDelta | null = null;
  children: ChildRow[] = [];
  chartSeries: PriceSeries[] = [];
  loading = true;
  error: string | null = null;

  ngOnInit() {
    this.assetId = this.route.snapshot.paramMap.get('asset');
    if (!this.assetId) {
      this.error = 'No asset specified.';
      this.loading = false;
      return;
    }

    const oneYearAgo = new Date();
    oneYearAgo.setFullYear(oneYearAgo.getFullYear() - 1);

    forkJoin({
      asset: this.assetService.getAssetDetails(this.assetId),
      delta: this.assetService.getLatestDelta(this.assetId),
      history: this.assetService.getHistory(this.assetId, oneYearAgo),
    }).subscribe({
      next: ({ asset, delta, history }) => {
        this.asset = asset;
        this.delta = delta;
        this.chartSeries = [{ name: asset.name, prices: history.prices, times: history.times }];
        if (asset.type === 'Portfolio') {
          forkJoin({
            resp: this.positionsService.getPositions(),
            holdings: this.assetService.getHoldings(this.assetId!),
          }).subscribe({
            next: ({ resp, holdings }) => {
              const holdingMap = new Map(holdings.map((h) => [h.assetId, h]));
              this.children = resp.positions.map((p) => ({
                position: p,
                holding: holdingMap.get(p.assetId) ?? null,
              }));
              this.loading = false;
            },
            error: () => { this.loading = false; },
          });
        } else {
          this.loading = false;
        }
      },
      error: () => {
        this.error = 'Failed to load asset data.';
        this.loading = false;
      },
    });
  }
}
