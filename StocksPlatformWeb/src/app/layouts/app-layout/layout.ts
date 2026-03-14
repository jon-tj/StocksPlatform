import { Component, inject, OnInit } from '@angular/core';
import { Router, RouterLink, RouterOutlet, NavigationEnd } from '@angular/router';
import { AuthService } from '../../services/auth.service';
import { ThemeService } from '../../services/theme.service';
import { filter } from 'rxjs';

@Component({
  selector: 'app-layout',
  imports: [RouterOutlet, RouterLink],
  templateUrl: './layout.html',
  styleUrl: './layout.css',
})
export class AppLayout implements OnInit {
  private auth = inject(AuthService);
  private router = inject(Router);
  private themeService = inject(ThemeService);

  displayName = '';
  isDark = true;
  showLogout = false;

  ngOnInit() {
    this.displayName = this.auth.getUser()?.displayName ?? '';
    this.isDark = this.themeService.theme === 'dark';
    this.showLogout = this.isAppPage();

    this.router.events.pipe(filter(e => e instanceof NavigationEnd)).subscribe(() => {
      this.showLogout = this.isAppPage();
      this.displayName = this.auth.getUser()?.displayName ?? '';
    });
  }

  private isAppPage(): boolean {
    const url = this.router.url.split('?')[0];
    return url !== '/login' && url !== '/register';
  }

  toggleTheme() {
    this.themeService.toggle();
    this.isDark = this.themeService.theme === 'dark';
  }

  logout() {
    this.auth.logout();
    this.router.navigate(['/login']);
  }
}
