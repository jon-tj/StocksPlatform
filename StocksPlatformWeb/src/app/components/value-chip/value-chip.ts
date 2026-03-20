import { Component, computed, inject, input } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { DecimalPipe } from '@angular/common';
import { CurrencyService } from '../../services/currency.service';

@Component({
  selector: 'app-value-chip',
  imports: [DecimalPipe],
  templateUrl: './value-chip.html',
  styleUrl: './value-chip.css',
})
export class ValueChip {
  private currencyService = inject(CurrencyService);

  /** The raw amount in the currency specified by `currency`. */
  amount = input.required<number | null>();

  /** ISO 4217 currency code the amount is denominated in (e.g. 'NOK', 'USD'). */
  currency = input.required<string>();

  /** Decimal format for DecimalPipe, e.g. '1.2-2'. Defaults to '1.2-2'. */
  format = input<string>('1.2-2');

  private preferredCurrency = toSignal(
    this.currencyService.preferredCurrency,
    { initialValue: this.currencyService.currentPreferredCurrency },
  );

  private rates = toSignal(
    this.currencyService.rates,
    { initialValue: this.currencyService.currentRates },
  );

  /** Converted amount in the preferred currency, or null if rates are missing. */
  readonly displayAmount = computed<number | null>(() => {
    const amt = this.amount();
    if (amt === null || amt === undefined) return null;
    return CurrencyService.convertWithRates(
      amt,
      this.currency(),
      this.preferredCurrency(),
      this.rates(),
    );
  });

  readonly displayCurrency = computed(() => this.preferredCurrency());

  /** True when the amount had to be converted (input currency ≠ preferred). */
  readonly wasConverted = computed(() =>
    this.currency().toUpperCase() !== this.preferredCurrency().toUpperCase()
  );
}
