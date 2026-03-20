import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { BehaviorSubject, Observable } from 'rxjs';
import { tap } from 'rxjs/operators';

const API = 'http://localhost:5156';
const PREF_CURRENCY_KEY = 'sp.preferredCurrency';
const DEFAULT_CURRENCY = 'USD';

/** X/USD rate record as returned by the backend: { "NOK/USD": 0.094, "EUR/USD": 1.08, ... } */
export type RatesRecord = Record<string, number>;

@Injectable({ providedIn: 'root' })
export class CurrencyService {
  private http = inject(HttpClient);

  private preferredCurrency$ = new BehaviorSubject<string>(this.readPreferredCurrency());

  /**
   * Each entry is "X/USD" -> rate, meaning 1 X = rate USD.
   * USD/USD = 1.0 is always present.
   */
  private rates$ = new BehaviorSubject<Map<string, number>>(new Map([['USD/USD', 1]]));

  // ── Observables ──────────────────────────────────────────────────────────

  readonly preferredCurrency: Observable<string> = this.preferredCurrency$.asObservable();

  readonly rates: Observable<Map<string, number>> = this.rates$.asObservable();

  // ── Synchronous accessors ─────────────────────────────────────────────────

  get currentPreferredCurrency(): string {
    return this.preferredCurrency$.value;
  }

  get currentRates(): Map<string, number> {
    return this.rates$.value;
  }

  // ── Mutations ─────────────────────────────────────────────────────────────

  setPreferredCurrency(code: string): void {
    const upper = code.toUpperCase();
    localStorage.setItem(PREF_CURRENCY_KEY, upper);
    this.preferredCurrency$.next(upper);
  }

  /**
   * Push a fresh rates snapshot (e.g. from backend).
   * USD/USD = 1 is always enforced.
   */
  setRates(raw: RatesRecord): void {
    const map = new Map<string, number>([['USD/USD', 1]]);
    for (const [pair, rate] of Object.entries(raw)) {
      map.set(pair.toUpperCase(), rate);
    }
    this.rates$.next(map);
  }

  /**
   * Stub — will call GET /api/currency/rates once the backend endpoint exists.
   * Returns an observable so callers can subscribe for error handling.
   */
  loadRates(): Observable<RatesRecord> {
    return this.http.get<RatesRecord>(`${API}/api/currency/rates`).pipe(
      tap(raw => this.setRates(raw)),
    );
  }

  // ── Conversion ────────────────────────────────────────────────────────────

  /**
   * Pure conversion using a rates map snapshot.
   * Returns null when rates for either currency are unavailable.
   * Both currencies are normalised to uppercase.
   */
  static convertWithRates(
    amount: number,
    fromCurrency: string,
    toCurrency: string,
    rates: Map<string, number>,
  ): number | null {
    const from = fromCurrency.toUpperCase();
    const to = toCurrency.toUpperCase();

    if (from === to) return amount;

    const fromRate = from === 'USD' ? 1 : rates.get(`${from}/USD`);
    const toRate = to === 'USD' ? 1 : rates.get(`${to}/USD`);

    if (fromRate === undefined || toRate === undefined) return null;

    // Convert via USD as pivot: amount(from) -> USD -> amount(to)
    return (amount * fromRate) / toRate;
  }

  // ── Private ───────────────────────────────────────────────────────────────

  private readPreferredCurrency(): string {
    return localStorage.getItem(PREF_CURRENCY_KEY) ?? DEFAULT_CURRENCY;
  }
}
