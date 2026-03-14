import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { catchError, map } from 'rxjs/operators';

const API = 'http://localhost:5156';
export const DEFAULT_ASSET_ID = '00000000-0000-0000-0000-000000000000';

export interface AssetDetails {
  id: string;
  name: string;
  type: 'Portfolio' | 'Stock' | 'Commodity' | 'Crypto';
  symbol?: string;
  market?: string;
  broker?: string;
  brokerSymbol?: string;
}

export interface AssetDelta {
  assetId: string;
  assetName: string;
  date: string;
  marketDelta: number;
  pairDelta: number | null;
  pairAssetId: string | null;
  publicSentimentDelta: number;
  memberSentimentDelta: number;
  fundamentalDelta: number;
  institutionalOrderFlowDelta: number;
  combinedScore: number;
}

export interface HoldingDelta extends AssetDelta {
  targetFraction: number;
}

export interface AssetHistory {
  prices: number[];
  times: string[];
}

@Injectable({ providedIn: 'root' })
export class AssetService {
  private http = inject(HttpClient);

  getMyAssets(): Observable<AssetDetails[]> {
    return this.http.get<AssetDetails[]>(`${API}/api/asset`);
  }

  getAssetDetails(id: string): Observable<AssetDetails> {
    return this.http.get<AssetDetails>(`${API}/api/asset/${id}`);
  }

  getLatestDelta(id: string): Observable<AssetDelta> {
    return this.http.get<AssetDelta>(`${API}/api/analysis/${id}/latest`);
  }

  getHoldings(portfolioId: string): Observable<HoldingDelta[]> {
    return this.http.get<HoldingDelta[]>(`${API}/api/analysis/${portfolioId}/holdings`);
  }

  getHistory(id: string, timeFrom?: Date): Observable<AssetHistory> {
    const params: Record<string, string> = {};
    if (timeFrom) {
      params['timeFrom'] = timeFrom.toISOString().split('T')[0];
    }
    return this.http.get<AssetHistory>(`${API}/api/asset/${id}/history`, { params });
  }

  getDeltaAt(id: string, date: string): Observable<AssetDelta | null> {
    return this.http.get<AssetDelta>(`${API}/api/analysis/${id}/at`, { params: { date }, observe: 'response' }).pipe(
      map(resp => resp.status === 204 ? null : resp.body),
      catchError(() => of(null)),
    );
  }
}
