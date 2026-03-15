import { Component, OnInit, OnDestroy, inject } from '@angular/core';
import { Router } from '@angular/router';
import { forkJoin, Subject, of } from 'rxjs';
import { debounceTime, distinctUntilChanged, switchMap } from 'rxjs/operators';
import { FormsModule } from '@angular/forms';
import { StockChart, PriceSeries } from '../../components/stock-chart/stock-chart';
import { PositionsList } from '../../components/positions-list/positions-list';
import { AssetService, AssetDetails, DEFAULT_ASSET_ID } from '../../services/asset.service';
import { Position, PositionsService } from '../../services/positions.service';

@Component({
  selector: 'app-dashboard',
  imports: [StockChart, PositionsList, FormsModule],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.css',
})
export class Dashboard implements OnInit, OnDestroy {
  private assetService = inject(AssetService);
  private positionsService = inject(PositionsService);
  private router = inject(Router);

  pollCompleted = false;
  chartSeries: PriceSeries[] = [];
  chartTitle = '';
  chartLoading = true;
  positions: Position[] = [];

  searchQuery = '';
  searchResults: AssetDetails[] = [];
  showDropdown = false;

  private searchInput$ = new Subject<string>();
  private searchSub = this.searchInput$.pipe(
    debounceTime(200),
    distinctUntilChanged(),
    switchMap(q => q.length >= 2 ? this.assetService.searchAssets(q) : of<AssetDetails[]>([])),
  ).subscribe(results => {
    this.searchResults = results;
    this.showDropdown = results.length > 0;
  });

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
        this.chartSeries = [{ name: details.name, prices: history.prices, times: history.times }];
        this.chartLoading = false;
        this.positions = positionsResp.positions;
        this.pollCompleted = !positionsResp.mock;
      },
      error: () => {
        this.chartLoading = false;
      },
    });
  }

  ngOnDestroy() {
    this.searchSub.unsubscribe();
  }

  onSearchInput() {
    this.searchInput$.next(this.searchQuery);
    if (this.searchQuery.length < 2) {
      this.searchResults = [];
      this.showDropdown = false;
    }
  }

  onSearchBlur() {
    setTimeout(() => { this.showDropdown = false; }, 150);
  }

  selectAsset(asset: AssetDetails) {
    this.searchQuery = '';
    this.searchResults = [];
    this.showDropdown = false;
    this.router.navigate(['/analysis', asset.id]);
  }
}
