import { Component, inject, OnInit } from '@angular/core';
import { Router, RouterLink, RouterOutlet } from '@angular/router';
import { AuthService } from '../../services/auth.service';
import { ThemeService } from '../../services/theme.service';

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

  ngOnInit() {
    this.displayName = this.auth.getUser()?.displayName ?? '';
    this.isDark = this.themeService.theme === 'dark';
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
