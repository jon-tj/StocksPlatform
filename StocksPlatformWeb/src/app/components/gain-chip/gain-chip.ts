import { Component, Input } from '@angular/core';
import { DecimalPipe } from '@angular/common';

@Component({
  selector: 'app-gain-chip',
  imports: [DecimalPipe],
  templateUrl: './gain-chip.html',
  styleUrl: './gain-chip.css',
})
export class GainChip {
  @Input() value: number | null = null;
  @Input() format = '1.2-2';

  get gainClass(): string {
    if (this.value === null || Math.abs(this.value) < 0.01) return 'gain-neutral';
    if (this.value >= 0.5)  return 'gain-strong-pos';
    if (this.value > 0)     return 'gain-muted-pos';
    if (this.value <= -0.5) return 'gain-strong-neg';
    return 'gain-muted-neg';
  }
}
