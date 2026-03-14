import { Component, Input } from '@angular/core';

export interface Position {
  symbol: string;
  sharesFraction: number;
  returnPercent: number;
}

@Component({
  selector: 'app-positions-list',
  imports: [],
  templateUrl: './positions-list.html',
  styleUrl: './positions-list.css',
})
export class PositionsList {
  @Input({ required: true }) positions!: Position[];
  @Input() authorized = true;
}
