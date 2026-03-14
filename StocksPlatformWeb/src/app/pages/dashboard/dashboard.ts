import { Component, OnInit, inject } from '@angular/core';
import { forkJoin } from 'rxjs';
import { StockChart, ReturnsSeries } from '../../components/stock-chart/stock-chart';
import { PositionsList } from '../../components/positions-list/positions-list';
import { AssetService, DEFAULT_ASSET_ID } from '../../services/asset.service';
import { Position, PositionsService } from '../../services/positions.service';

@Component({
  selector: 'app-dashboard',
  imports: [StockChart, PositionsList],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.css',
})
export class Dashboard implements OnInit {
  private assetService = inject(AssetService);
  private positionsService = inject(PositionsService);

  pollCompleted = false;
  chartSeries: ReturnsSeries[] = [];
  chartTitle = '';
  chartLoading = true;
  positions: Position[] = [];

  ngOnInit() {
    const oneYearAgo = new Date();
    oneYearAgo.setFullYear(oneYearAgo.getFullYear() - 1);

    forkJoin({
      details: this.assetService.getAssetDetails(DEFAULT_ASSET_ID),
      history: this.assetService.getHistory(DEFAULT_ASSET_ID, oneYearAgo),
      positionsResp: this.positionsService.getPositions(),
    }).subscribe({
      next: ({ details, history, positionsResp }) => {
        this.chartTitle = details.name;
        this.chartSeries = [{ name: details.name, returns: history.returns, times: history.times }];
        this.chartLoading = false;
        this.positions = positionsResp.positions;
        this.pollCompleted = !positionsResp.mock;
      },
      error: () => {
        this.chartLoading = false;
      },
    });
  }
}
