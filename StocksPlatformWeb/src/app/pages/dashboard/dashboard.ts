import { Component, OnInit, inject } from '@angular/core';
import { forkJoin } from 'rxjs';
import { StockChart, ReturnsSeries } from '../../components/stock-chart/stock-chart';
import { PositionsList, Position } from '../../components/positions-list/positions-list';
import { AssetService, DEFAULT_ASSET_ID } from '../../services/asset.service';

@Component({
  selector: 'app-dashboard',
  imports: [StockChart, PositionsList],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.css',
})
export class Dashboard implements OnInit {
  private assetService = inject(AssetService);

  pollCompleted = false;
  chartSeries: ReturnsSeries[] = [];
  chartTitle = '';
  chartLoading = true;

  positions: Position[] = [
    { symbol: 'NVDA', sharesFraction: 18.4, returnPercent: 12.3 },
    { symbol: 'MSFT', sharesFraction: 14.7, returnPercent: 6.8 },
    { symbol: 'AAPL', sharesFraction: 12.1, returnPercent: 3.2 },
    { symbol: 'META', sharesFraction: 9.8, returnPercent: 15.1 },
    { symbol: 'AMZN', sharesFraction: 8.3, returnPercent: 7.4 },
  ];

  ngOnInit() {
    const oneYearAgo = new Date();
    oneYearAgo.setFullYear(oneYearAgo.getFullYear() - 1);

    forkJoin({
      details: this.assetService.getAssetDetails(DEFAULT_ASSET_ID),
      history: this.assetService.getHistory(DEFAULT_ASSET_ID, oneYearAgo),
    }).subscribe({
      next: ({ details, history }) => {
        this.chartTitle = details.name;
        this.chartSeries = [{ name: details.name, returns: history.returns, times: history.times }];
        this.chartLoading = false;
      },
      error: () => {
        this.chartLoading = false;
      },
    });
  }
}
