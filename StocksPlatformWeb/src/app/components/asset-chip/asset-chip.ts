import { Component, input } from '@angular/core';

@Component({
  selector: 'app-asset-chip',
  templateUrl: './asset-chip.html',
  styleUrl: './asset-chip.css',
})
export class AssetChip {
  name = input.required<string>();
  symbol = input<string | undefined>(undefined);
  iconUrl = input<string | undefined>(undefined);
}
