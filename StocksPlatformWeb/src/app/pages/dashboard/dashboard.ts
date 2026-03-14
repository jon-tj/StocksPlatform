import { Component, OnInit, inject } from '@angular/core';
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

    this.assetService.getHistory(DEFAULT_ASSET_ID, oneYearAgo).subscribe({
      next: (data) => {
        this.chartSeries = [{ name: 'Portfolio', returns: data.returns, times: data.times }];
        this.chartLoading = false;
      },
      error: () => {
        this.chartLoading = false;
      },
    });
  }
}
