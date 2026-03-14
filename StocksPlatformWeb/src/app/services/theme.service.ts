import { Injectable } from '@angular/core';

type Theme = 'dark' | 'light';

const COOKIE_NAME = 'theme';
const COOKIE_MAX_AGE = 60 * 60 * 24 * 365; // 1 year

@Injectable({ providedIn: 'root' })
export class ThemeService {
  private _theme: Theme;

  constructor() {
    const saved = this.readCookie();
    this._theme = saved === 'light' ? 'light' : 'dark';
    this.applyTheme();
  }

  get theme(): Theme {
    return this._theme;
  }

  toggle(): void {
    this._theme = this._theme === 'dark' ? 'light' : 'dark';
    this.applyTheme();
    this.writeCookie();
  }

  private applyTheme(): void {
    document.documentElement.setAttribute('data-theme', this._theme);
  }

  private readCookie(): string {
    const match = document.cookie.match(/(?:^|;\s*)theme=([^;]*)/);
    return match ? decodeURIComponent(match[1]) : '';
  }

  private writeCookie(): void {
    document.cookie = `${COOKIE_NAME}=${this._theme}; max-age=${COOKIE_MAX_AGE}; path=/; SameSite=Strict`;
  }
}
