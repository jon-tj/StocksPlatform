import { Component, input } from '@angular/core';
import { DecimalPipe } from '@angular/common';

@Component({
  selector: 'app-value-chip',
  imports: [DecimalPipe],
  templateUrl: './value-chip.html',
  styleUrl: './value-chip.css',
})
export class ValueChip {
  /** The raw amount in the currency specified by `currency`. */
  amount = input.required<number | null>();

  /** ISO 4217 currency code the amount is denominated in (e.g. 'NOK', 'USD'). */
  currency = input.required<string>();

  /** Decimal format for DecimalPipe, e.g. '1.2-2'. Defaults to '1.2-2'. */
  format = input<string>('1.2-2');
}
