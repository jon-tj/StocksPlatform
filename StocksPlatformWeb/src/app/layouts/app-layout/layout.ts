import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink, RouterOutlet, NavigationEnd } from '@angular/router';
import { Subject, debounceTime, distinctUntilChanged, filter, interval, of, startWith, switchMap, takeUntil } from 'rxjs';
import { SectorLabelPipe } from '../../pipes/sector-label.pipe';
import { AssetDetails, AssetService, LivePrice } from '../../services/asset.service';
import { AuthService } from '../../services/auth.service';
import { ThemeService } from '../../services/theme.service';
import { DecimalPipe } from '@angular/common';
import { AssetChip } from '../../components/asset-chip/asset-chip';
import { ValueChip } from '../../components/value-chip/value-chip';
interface RecentAsset {
  id: string;
  name: string;
  symbol?: string;
  iconUrl?: string;
  currency?: string;
  type?: string;
  sector?: string;
}

type DragList = 'starred' | 'recent';
type RecentSortMode = 'time' | 'alphabet';

const RECENT_ASSETS_KEY = 'sp.recentAssets';
const RECENT_ASSETS_LIMIT = 5;
const RECENT_SORT_MODE_KEY = 'sp.recentSortMode';

@Component({
  selector: 'app-layout',
  imports: [RouterOutlet, RouterLink, FormsModule, SectorLabelPipe, DecimalPipe, AssetChip, ValueChip],
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
  recentSortMode: RecentSortMode = 'time';
  currentAnalysisAssetId: string | null = null;
  draggedAsset: RecentAsset | null = null;
  draggedFrom: DragList | null = null;
  dropTarget: DragList | null = null;
  liveByAssetId = new Map<string, LivePrice>();

  get recentAssetsDeduped(): RecentAsset[] {
    const starredIds = new Set(this.starredAssets.map(a => a.id));
    const deduped = this.recentAssets.filter(a => !starredIds.has(a.id));

    if (this.recentSortMode === 'alphabet') {
      return [...deduped].sort((a, b) =>
        a.name.localeCompare(b.name, undefined, { sensitivity: 'base' })
      );
    }

    return deduped;
  }

  ngOnInit() {
    this.displayName = this.auth.getUser()?.displayName ?? '';
    this.isDark = this.themeService.theme === 'dark';
    this.showLogout = this.isAppPage();
    this.recentSortMode = this.readRecentSortMode();
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

    this.startSidebarLivePriceStream();
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
    this.addRecentAsset({ id: asset.id, name: asset.name, symbol: asset.symbol, iconUrl: asset.iconUrl, currency: asset.currency, type: asset.type, sector: asset.sector });
    this.router.navigate(['/analysis', asset.id]);
  }

  openRecentAsset(assetId: string) {
    this.router.navigate(['/analysis', assetId]);
  }

  setRecentSortMode(mode: RecentSortMode): void {
    if (this.recentSortMode === mode) return;
    this.recentSortMode = mode;
    this.writeRecentSortMode(mode);
  }

  onAssetDragStart(asset: RecentAsset, from: DragList, event: DragEvent): void {
    this.draggedAsset = asset;
    this.draggedFrom = from;
    if (event.dataTransfer) {
      event.dataTransfer.effectAllowed = 'move';
      event.dataTransfer.setData('text/plain', asset.id);
    }
  }

  onAssetDragEnd(): void {
    this.draggedAsset = null;
    this.draggedFrom = null;
    this.dropTarget = null;
  }

  onDragOver(event: DragEvent, target: DragList): void {
    event.preventDefault();
    this.dropTarget = target;
    if (event.dataTransfer) {
      event.dataTransfer.dropEffect = 'move';
    }
  }

  onDragLeave(target: DragList): void {
    if (this.dropTarget === target) {
      this.dropTarget = null;
    }
  }

  onDrop(event: DragEvent, target: DragList): void {
    event.preventDefault();

    const asset = this.draggedAsset;
    const from = this.draggedFrom;

    this.dropTarget = null;

    if (!asset || !from || from === target) return;

    if (target === 'starred') {
      if (this.starredAssets.some(a => a.id === asset.id)) return;

      this.assetService.starAsset(asset.id).pipe(takeUntil(this.destroy$)).subscribe({
        next: () => {
          this.addRecentAsset(asset);
        },
        error: () => { },
      });
      return;
    }

    if (!this.starredAssets.some(a => a.id === asset.id)) return;

    this.assetService.unstarAsset(asset.id).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => {
        this.addRecentAsset(asset);
      },
      error: () => { },
    });
  }

  isActiveAsset(assetId: string): boolean {
    return this.currentAnalysisAssetId === assetId;
  }

  getSidebarLive(assetId: string): LivePrice | null {
    return this.liveByAssetId.get(this.normalizeAssetId(assetId)) ?? null;
  }

  private startSidebarLivePriceStream(): void {
    interval(15000).pipe(
      startWith(0),
      switchMap(() => {
        const ids = this.getSidebarAssetIds();
        if (ids.length === 0) {
          this.liveByAssetId.clear();
          return of([] as LivePrice[]);
        }

        return this.assetService.getLivePrices(ids);
      }),
      takeUntil(this.destroy$),
    ).subscribe({
      next: (prices) => {
        const next = new Map<string, LivePrice>();
        for (const p of prices) {
          next.set(this.normalizeAssetId(p.assetId), p);
        }
        this.liveByAssetId = next;
      },
      error: () => {
        this.liveByAssetId.clear();
      },
    });
  }

  private getSidebarAssetIds(): string[] {
    const ids = new Set<string>();

    for (const asset of this.starredAssets) {
      ids.add(this.normalizeAssetId(asset.id));
    }

    for (const asset of this.recentAssetsDeduped) {
      ids.add(this.normalizeAssetId(asset.id));
    }

    return Array.from(ids);
  }

  private normalizeAssetId(assetId: string): string {
    return assetId.trim().toLowerCase();
  }

  private captureRecentFromUrl(url: string): void {
    const path = url.split('?')[0];
    const parts = path.split('/').filter(Boolean);
    if (parts.length < 2 || parts[0] !== 'analysis') {
      this.currentAnalysisAssetId = null;
      return;
    }

    const assetId = parts[1];
    this.currentAnalysisAssetId = assetId;
    const existing = this.recentAssets.find(a => a.id === assetId);
    // Immediately show whatever we have (name/symbol at minimum)
    this.addRecentAsset(existing ?? { id: assetId, name: assetId.slice(0, 8) });
    // Always fetch fresh to ensure iconUrl and other fields are up to date
    this.assetService.getAssetDetails(assetId).pipe(takeUntil(this.destroy$)).subscribe({
      next: (asset) => this.addRecentAsset({ id: asset.id, name: asset.name, symbol: asset.symbol, iconUrl: asset.iconUrl, currency: asset.currency, type: asset.type, sector: asset.sector }),
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
          iconUrl: a.iconUrl,
          currency: a.currency,
          type: a.type,
          sector: a.sector,
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

  private readRecentSortMode(): RecentSortMode {
    const raw = localStorage.getItem(RECENT_SORT_MODE_KEY);
    if (raw === 'alphabet' || raw === 'time') return raw;
    return 'time';
  }

  private writeRecentSortMode(mode: RecentSortMode): void {
    localStorage.setItem(RECENT_SORT_MODE_KEY, mode);
  }

  logout() {
    this.auth.logout();
    this.router.navigate(['/login']);
  }
}
