import { Component, inject, OnInit } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { Router } from '@angular/router';
import { AuthService } from '../../services/auth.service';

interface Notification {
  id: number;
  type: 'poll' | 'info' | 'alert';
  title: string;
  body: string;
  time: string;
  read: boolean;
}

interface ReturnPoint {
  month: string;
  value: number;
}

interface Holding {
  ticker: string;
  name: string;
  weight: number;
  return: number;
}

@Component({
  selector: 'app-dashboard',
  imports: [DecimalPipe],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.css',
})
export class Dashboard implements OnInit {
  private auth = inject(AuthService);
  private router = inject(Router);

  user: { email: string; displayName: string } | null = null;
  memberSince = 'March 2026';

  notifications: Notification[] = [
    {
      id: 1,
      type: 'poll',
      title: 'Weekly Poll Live',
      body: "This week's stock prediction poll is now open. Your forecast counts!",
      time: '2h ago',
      read: false,
    },
    {
      id: 2,
      type: 'poll',
      title: "Last Week's Results",
      body: 'Results for the Mar 7 poll are in. You scored 81% accuracy.',
      time: '1d ago',
      read: false,
    },
    {
      id: 3,
      type: 'info',
      title: 'Portfolio Rebalanced',
      body: 'Your virtual portfolio was rebalanced based on the latest alpha scores.',
      time: '3d ago',
      read: true,
    },
    {
      id: 4,
      type: 'alert',
      title: 'High Signal Divergence',
      body: 'NVDA shows high disagreement between institutional flow and sentiment signals.',
      time: '5d ago',
      read: true,
    },
  ];

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

  holdings: Holding[] = [
    { ticker: 'NVDA', name: 'NVIDIA Corporation', weight: 18.4, return: 12.3 },
    { ticker: 'MSFT', name: 'Microsoft Corporation', weight: 14.7, return: 6.8 },
    { ticker: 'AAPL', name: 'Apple Inc.', weight: 12.1, return: 3.2 },
    { ticker: 'META', name: 'Meta Platforms', weight: 9.8, return: 15.1 },
    { ticker: 'AMZN', name: 'Amazon.com Inc.', weight: 8.3, return: 7.4 },
  ];

  readonly chartW = 500;
  readonly chartH = 130;

  get chartLinePath(): string {
    return this.buildPath(false);
  }

  get chartAreaPath(): string {
    return this.buildPath(true);
  }

  private buildPath(close: boolean): string {
    const vals = this.returns.map((r) => r.value);
    const min = Math.min(...vals);
    const max = Math.max(...vals);
    const range = max - min || 1;
    const pad = 12;
    const xStep = this.chartW / (vals.length - 1);
    const pts = vals.map((v, i) => {
      const x = +(i * xStep).toFixed(1);
      const y = +(pad + (1 - (v - min) / range) * (this.chartH - pad * 2)).toFixed(1);
      return `${x},${y}`;
    });
    const line = `M ${pts.join(' L ')}`;
    return close ? `${line} L ${this.chartW},${this.chartH} L 0,${this.chartH} Z` : line;
  }

  get totalReturn(): number {
    return +this.returns.reduce((s, r) => s + r.value, 0).toFixed(1);
  }

  get unreadCount(): number {
    return this.notifications.filter((n) => !n.read).length;
  }

  get initials(): string {
    return (this.user?.displayName ?? '?')
      .split(' ')
      .map((w) => w[0])
      .join('')
      .toUpperCase()
      .slice(0, 2);
  }

  ngOnInit() {
    this.user = this.auth.getUser();
  }

  markAllRead() {
    this.notifications = this.notifications.map((n) => ({ ...n, read: true }));
  }

  logout() {
    this.auth.logout();
    this.router.navigate(['/login']);
  }
}
