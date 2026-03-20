import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink, RouterOutlet, NavigationEnd } from '@angular/router';
import { Subject, debounceTime, distinctUntilChanged, filter, of, switchMap, takeUntil } from 'rxjs';
import { SectorLabelPipe } from '../../pipes/sector-label.pipe';
import { AssetDetails, AssetService } from '../../services/asset.service';
import { AuthService } from '../../services/auth.service';
import { ThemeService } from '../../services/theme.service';

interface RecentAsset {
  id: string;
  name: string;
  symbol?: string;
}

const RECENT_ASSETS_KEY = 'sp.recentAssets';
const RECENT_ASSETS_LIMIT = 5;

@Component({
  selector: 'app-layout',
  imports: [RouterOutlet, RouterLink, FormsModule, SectorLabelPipe],
  templateUrl: './layout.html',
  styleUrl: './layout.css',
})
export class AppLayout implements OnInit, OnDestroy {
  private auth = inject(AuthService);
  private router = inject(Router);
  private themeService = inject(ThemeService);
  private assetService = inject(AssetService);
  private destroy$ = new Subject<void>();
  private searchInput$ = new Subject<string>();

  displayName = '';
  isDark = true;
  showLogout = false;
  searchQuery = '';
  searchResults: AssetDetails[] = [];
  showSearchDropdown = false;
  starredAssets: RecentAsset[] = [];
  recentAssets: RecentAsset[] = [];

  get recentAssetsDeduped(): RecentAsset[] {
    const starredIds = new Set(this.starredAssets.map(a => a.id));
    return this.recentAssets.filter(a => !starredIds.has(a.id));
  }

  ngOnInit() {
    this.displayName = this.auth.getUser()?.displayName ?? '';
    this.isDark = this.themeService.theme === 'dark';
    this.showLogout = this.isAppPage();
    this.loadStarredAssets();
    this.recentAssets = this.readRecentAssets();
    this.captureRecentFromUrl(this.router.url);

    this.router.events.pipe(
      filter(e => e instanceof NavigationEnd),
      takeUntil(this.destroy$),
    ).subscribe((event) => {
      this.showLogout = this.isAppPage();
      this.displayName = this.auth.getUser()?.displayName ?? '';
      this.showSearchDropdown = false;
      this.captureRecentFromUrl((event as NavigationEnd).urlAfterRedirects);
    });

    this.searchInput$.pipe(
      debounceTime(200),
      distinctUntilChanged(),
      switchMap(q => q.length >= 2 ? this.assetService.searchAssets(q) : of<AssetDetails[]>([])),
      takeUntil(this.destroy$),
    ).subscribe(results => {
      this.searchResults = results;
      this.showSearchDropdown = results.length > 0;
    });

    this.assetService.starredChanges
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => this.loadStarredAssets());
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private isAppPage(): boolean {
    const url = this.router.url.split('?')[0];
    return url !== '/login' && url !== '/register';
  }

  toggleTheme() {
    this.themeService.toggle();
    this.isDark = this.themeService.theme === 'dark';
  }

  onSearchInput() {
    this.searchInput$.next(this.searchQuery);
    if (this.searchQuery.length < 2) {
      this.searchResults = [];
      this.showSearchDropdown = false;
    }
  }

  onSearchBlur() {
    setTimeout(() => { this.showSearchDropdown = false; }, 150);
  }

  selectAsset(asset: AssetDetails) {
    this.searchQuery = '';
    this.searchResults = [];
    this.showSearchDropdown = false;
    this.addRecentAsset({ id: asset.id, name: asset.name, symbol: asset.symbol });
    this.router.navigate(['/analysis', asset.id]);
  }

  openRecentAsset(assetId: string) {
    this.router.navigate(['/analysis', assetId]);
  }

  private captureRecentFromUrl(url: string): void {
    const path = url.split('?')[0];
    const parts = path.split('/').filter(Boolean);
    if (parts.length < 2 || parts[0] !== 'analysis') return;

    const assetId = parts[1];
    const existing = this.recentAssets.find(a => a.id === assetId);
    if (existing) {
      this.addRecentAsset(existing);
      return;
    }

    this.addRecentAsset({ id: assetId, name: assetId.slice(0, 8) });

    this.assetService.getAssetDetails(assetId).pipe(takeUntil(this.destroy$)).subscribe({
      next: (asset) => this.addRecentAsset({ id: asset.id, name: asset.name, symbol: asset.symbol }),
      error: () => { },
    });
  }

  private addRecentAsset(asset: RecentAsset): void {
    this.recentAssets = [asset, ...this.recentAssets.filter(a => a.id !== asset.id)]
      .slice(0, RECENT_ASSETS_LIMIT);
    this.writeRecentAssets(this.recentAssets);
  }

  private loadStarredAssets(): void {
    this.assetService.getStarredAssets().pipe(takeUntil(this.destroy$)).subscribe({
      next: (assets) => {
        this.starredAssets = assets.map(a => ({
          id: a.id,
          name: a.name,
          symbol: a.symbol,
        }));
      },
      error: () => {
        this.starredAssets = [];
      },
    });
  }

  private readRecentAssets(): RecentAsset[] {
    const raw = localStorage.getItem(RECENT_ASSETS_KEY);
    if (!raw) return [];

    try {
      const parsed = JSON.parse(raw) as RecentAsset[];
      if (!Array.isArray(parsed)) return [];
      return parsed
        .filter(item => !!item && typeof item.id === 'string' && typeof item.name === 'string')
        .slice(0, RECENT_ASSETS_LIMIT);
    } catch {
      return [];
    }
  }

  private writeRecentAssets(assets: RecentAsset[]): void {
    localStorage.setItem(RECENT_ASSETS_KEY, JSON.stringify(assets));
  }

  logout() {
    this.auth.logout();
    this.router.navigate(['/login']);
  }
}
