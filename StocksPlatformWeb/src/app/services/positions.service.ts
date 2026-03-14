import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

const API = 'http://localhost:5156';

export interface Position {
  symbol: string;
  fraction: number;
  returnPercent: number;
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
}
