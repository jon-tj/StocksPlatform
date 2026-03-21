import { Component, computed, input } from '@angular/core';

@Component({
  selector: 'app-asset-chip',
  templateUrl: './asset-chip.html',
  styleUrl: './asset-chip.css',
})
export class AssetChip {
  name = input.required<string>();
  symbol = input<string | undefined>(undefined);
  iconUrl = input<string | undefined>(undefined);
  type = input<string | undefined>(undefined);
  sector = input<string | undefined>(undefined);

  /** Material icon name to show when no iconUrl is available. */
  readonly fallbackIcon = computed(() => {
    const t = this.type()?.toLowerCase();
    const s = this.sector()?.toLowerCase() ?? '';

    if (t === 'portfolio') return 'folder';
    if (t === 'crypto')    return 'currency_bitcoin';
    if (t === 'commodity') return 'oil_barrel';
    if (t === 'currency')  return 'savings';

    // Stock: map common sectors to relevant icons
    if (t === 'stock' || t === undefined) {
      if (s.includes('tech') || s.includes('software') || s.includes('semiconductor')) return 'computer';
      if (s.includes('health') || s.includes('pharma') || s.includes('biotech'))       return 'health_and_safety';
      if (s.includes('energy') || s.includes('oil') || s.includes('gas'))              return 'bolt';
      if (s.includes('finance') || s.includes('bank') || s.includes('insurance'))      return 'account_balance';
      if (s.includes('consumer') || s.includes('retail'))                              return 'shopping_bag';
      if (s.includes('industrial') || s.includes('manufactur') || s.includes('aerospace')) return 'factory';
      if (s.includes('material') || s.includes('mining') || s.includes('chemical'))   return 'diamond';
      if (s.includes('real_estate') || s.includes('real estate') || s.includes('reit')) return 'apartment';
      if (s.includes('util'))                                                           return 'electric_bolt';
      if (s.includes('telecom') || s.includes('communication') || s.includes('media')) return 'cell_tower';
      return 'factory'; // default stock fallback
    }

    return 'work';
  });
}
