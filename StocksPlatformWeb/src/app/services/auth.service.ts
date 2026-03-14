import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';

const API = 'http://localhost:5156';

interface AuthResponse {
  accessToken: string;
  email: string;
  displayName: string;
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private http = inject(HttpClient);

  register(email: string, password: string, displayName: string): Observable<AuthResponse> {
    return this.http
      .post<AuthResponse>(`${API}/api/auth/register`, { email, password, displayName })
      .pipe(
        tap((res) => {
          localStorage.setItem('token', res.accessToken);
          localStorage.setItem(
            'user',
            JSON.stringify({ email: res.email, displayName: res.displayName }),
          );
        }),
      );
  }

  login(email: string, password: string): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${API}/api/auth/login`, { email, password }).pipe(
      tap((res) => {
        localStorage.setItem('token', res.accessToken);
        localStorage.setItem('user', JSON.stringify({ email: res.email, displayName: res.displayName }));
      }),
    );
  }

  logout(): void {
    localStorage.removeItem('token');
    localStorage.removeItem('user');
  }

  isLoggedIn(): boolean {
    const token = localStorage.getItem('token');
    if (!token) return false;
    try {
      const payload = JSON.parse(atob(token.split('.')[1]));
      return payload.exp * 1000 > Date.now();
    } catch {
      return false;
    }
  }

  getUser(): { email: string; displayName: string } | null {
    const raw = localStorage.getItem('user');
    return raw ? JSON.parse(raw) : null;
  }
}
