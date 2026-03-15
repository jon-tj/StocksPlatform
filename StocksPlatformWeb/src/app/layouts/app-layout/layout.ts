import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink, RouterOutlet, NavigationEnd } from '@angular/router';
import { Subject, debounceTime, distinctUntilChanged, filter, of, switchMap, takeUntil } from 'rxjs';
import { SectorLabelPipe } from '../../pipes/sector-label.pipe';
import { AssetDetails, AssetService } from '../../services/asset.service';
import { AuthService } from '../../services/auth.service';
import { ThemeService } from '../../services/theme.service';

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

  ngOnInit() {
    this.displayName = this.auth.getUser()?.displayName ?? '';
    this.isDark = this.themeService.theme === 'dark';
    this.showLogout = this.isAppPage();

    this.router.events.pipe(
      filter(e => e instanceof NavigationEnd),
      takeUntil(this.destroy$),
    ).subscribe(() => {
      this.showLogout = this.isAppPage();
      this.displayName = this.auth.getUser()?.displayName ?? '';
      this.showSearchDropdown = false;
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
    this.router.navigate(['/analysis', asset.id]);
  }

  logout() {
    this.auth.logout();
    this.router.navigate(['/login']);
  }
}
