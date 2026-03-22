import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, Subject, of } from 'rxjs';
import { catchError, map, tap } from 'rxjs/operators';

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
  country?: string;
  region?: string;
  sector?: string;
  subsector?: string;
  iconUrl?: string;
  websiteUrl?: string;
  description?: string;
  ceo?: string;
  address1?: string;
  address2?: string;
  numberShares?: number;
  currency?: string;
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
  patternDelta: number;
  combinedScore: number;
}

export interface HoldingDelta extends AssetDelta {
  targetFraction: number;
}

export interface FundHoldingSnapshot {
  date: string;
  meanFundPercentage: number;
  medianFundPercentage: number;
  numFundsRepresented: number;
}

export interface LivePrice {
  assetId: string;
  symbol: string;
  price: number;
  dayGainPercent: number | null;
  fetchedAt: string;
  expiresAt: string;
}

export interface AssetHistory {
  prices: number[];
  times: string[];
}

export interface SentimentItem {
  title: string;
  body: string;
  date: string;
}

export interface PublicSentiment {
  nordnetComments: SentimentItem[];
  nordnetNews: SentimentItem[];
  e24News: SentimentItem[];
  companyNews: SentimentItem[];
}

export interface OrderBookLevel {
  bid: number;
  bidVol: number;
  ask: number;
  askVol: number;
}

export interface OrderBookSnapshotDto {
  timestamp: string;
  level: number;
  side: 'Bid' | 'Ask';
  price: number;
  newVol: number;
  increment: number;
}

@Injectable({ providedIn: 'root' })
export class AssetService {
  private http = inject(HttpClient);
  private starredChanged$ = new Subject<void>();

  get starredChanges(): Observable<void> {
    return this.starredChanged$.asObservable();
  }

  getMyAssets(): Observable<AssetDetails[]> {
    return this.http.get<AssetDetails[]>(`${API}/api/asset`);
  }

  getStarredAssets(): Observable<AssetDetails[]> {
    return this.http.get<AssetDetails[]>(`${API}/api/asset/starred`);
  }

  isAssetStarred(id: string): Observable<boolean> {
    return this.http.get<boolean>(`${API}/api/asset/${id}/starred`);
  }

  starAsset(id: string): Observable<void> {
    return this.http.post<void>(`${API}/api/asset/${id}/star`, {}).pipe(
      tap(() => this.starredChanged$.next()),
    );
  }

  unstarAsset(id: string): Observable<void> {
    return this.http.delete<void>(`${API}/api/asset/${id}/star`).pipe(
      tap(() => this.starredChanged$.next()),
    );
  }

  searchAssets(q: string): Observable<AssetDetails[]> {
    return this.http.get<AssetDetails[]>(`${API}/api/asset/search`, { params: { q } });
  }

  getAssetDetails(id: string): Observable<AssetDetails> {
    return this.http.get<AssetDetails>(`${API}/api/asset/${id}`);
  }

  getLatestDelta(id: string): Observable<AssetDelta> {
    return this.http.get<AssetDelta>(`${API}/api/analysis/${id}/latest`);
  }

  refreshLatestDelta(id: string): Observable<AssetDelta> {
    return this.http.get<AssetDelta>(`${API}/api/analysis/${id}/latest`, { params: { skipCache: 'true' } });
  }

  getHoldings(portfolioId: string): Observable<HoldingDelta[]> {
    return this.http.get<HoldingDelta[]>(`${API}/api/analysis/${portfolioId}/holdings`);
  }

  getInstitutionalSnapshots(assetId: string): Observable<FundHoldingSnapshot[]> {
    return this.http.get<FundHoldingSnapshot[]>(`${API}/api/analysis/${assetId}/institutional-snapshots`);
  }

  getLivePrice(assetId: string): Observable<LivePrice> {
    return this.http.get<LivePrice>(`${API}/api/price/live/${assetId}`);
  }

  getLivePrices(assetIds: string[]): Observable<LivePrice[]> {
    return this.http.get<LivePrice[]>(`${API}/api/price/live`, { params: { assetIds } });
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

  getSentiment(id: string): Observable<PublicSentiment> {
    return this.http.get<PublicSentiment>(`${API}/api/sentiment/${id}`);
  }

  getOrderBook(id: string, limit = 20): Observable<OrderBookLevel[]> {
    return this.http.get<OrderBookLevel[]>(`${API}/api/orderbook/${id}`, { params: { limit } });
  }

  getOrderBookHistory(
    id: string,
    options: { from?: string; to?: string; level?: number; side?: 'Bid' | 'Ask' } = {}
  ): Observable<OrderBookSnapshotDto[]> {
    const params: Record<string, string | number> = {};
    if (options.from)  params['from']  = options.from;
    if (options.to)    params['to']    = options.to;
    if (options.level) params['level'] = options.level;
    if (options.side)  params['side']  = options.side;
    return this.http.get<OrderBookSnapshotDto[]>(`${API}/api/orderbook/${id}/history`, { params });
  }
}
