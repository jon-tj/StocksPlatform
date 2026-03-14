import { Component, OnInit } from '@angular/core';
import { StockChart, ReturnsSeries } from '../../components/stock-chart/stock-chart';
import { PositionsList, Position } from '../../components/positions-list/positions-list';

interface ReturnPoint {
  month: string;
  value: number;
}

@Component({
  selector: 'app-dashboard',
  imports: [StockChart, PositionsList],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.css',
})
export class Dashboard implements OnInit {
  // Stub — replace with real poll-completion check from backend
  pollCompleted = false;

  returns: ReturnPoint[] = [
    { month: 'Apr', value: 2.1 },
    { month: 'May', value: -0.8 },
    { month: 'Jun', value: 3.4 },
    { month: 'Jul', value: 1.9 },
    { month: 'Aug', value: -1.2 },
    { month: 'Sep', value: 4.1 },
    { month: 'Oct', value: 2.7 },
    { month: 'Nov', value: 5.3 },
    { month: 'Dec', value: -0.3 },
    { month: 'Jan', value: 3.8 },
    { month: 'Feb', value: 1.5 },
    { month: 'Mar', value: 4.2 },
  ];

  positions: Position[] = [
    { symbol: 'NVDA', sharesFraction: 18.4, returnPercent: 12.3 },
    { symbol: 'MSFT', sharesFraction: 14.7, returnPercent: 6.8 },
    { symbol: 'AAPL', sharesFraction: 12.1, returnPercent: 3.2 },
    { symbol: 'META', sharesFraction: 9.8, returnPercent: 15.1 },
    { symbol: 'AMZN', sharesFraction: 8.3, returnPercent: 7.4 },
  ];

  get chartSeries(): ReturnsSeries[] {
    return [
      {
        name: 'Portfolio',
        returns: this.returns.map((r) => r.value),
        times: this.returns.map((r) => r.month),
      },
    ];
  }

  ngOnInit() {}

  // placeholder for future data loading
}
