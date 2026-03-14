import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

const API = 'http://localhost:5156';
export const DEFAULT_ASSET_ID = '00000000-0000-0000-0000-000000000000';

export interface AssetDetails {
  id: string;
  name: string;
  type: 'Portfolio' | 'Stock' | 'Commodity' | 'Crypto';
}

export interface AssetHistory {
  returns: number[];
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

  getHistory(id: string, timeFrom?: Date): Observable<AssetHistory> {
    const params: Record<string, string> = {};
    if (timeFrom) {
      params['timeFrom'] = timeFrom.toISOString().split('T')[0];
    }
    return this.http.get<AssetHistory>(`${API}/api/asset/${id}/history`, { params });
  }
}
