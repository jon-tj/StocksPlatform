import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

const API = 'http://localhost:5156';

export interface Position {
  assetId: string;
  symbol: string;
  name: string;
  quantity: number;
  fraction: number;
  returnPercent: number;
  iconUrl?: string;
  sector?: string;
  region?: string;
}

export interface PositionsResponse {
  positions: Position[];
  mock: boolean;
}

@Injectable({ providedIn: 'root' })
export class PositionsService {
  private http = inject(HttpClient);

  getPositions(): Observable<PositionsResponse> {
    return this.http.get<PositionsResponse>(`${API}/api/positions`);
  }

  getPortfolioPositions(portfolioId: string): Observable<Position[]> {
    return this.http.get<Position[]>(`${API}/api/positions/${portfolioId}`);
  }

  updatePortfolioQuantities(portfolioId: string, updates: { assetId: string; quantity: number }[]): Observable<void> {
    return this.http.patch<void>(`${API}/api/positions/${portfolioId}/quantities`, updates);
  }

  getPortfolioRemainder(portfolioId: string): Observable<number> {
    return this.http.get<number>(`${API}/api/positions/${portfolioId}/remainder`);
  }

  setPortfolioRemainder(portfolioId: string, value: number): Observable<void> {
    return this.http.put<void>(`${API}/api/positions/${portfolioId}/remainder`, value);
  }
}
