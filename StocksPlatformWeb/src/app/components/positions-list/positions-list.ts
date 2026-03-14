import { Component, Input, inject } from '@angular/core';
import { Router, RouterLink } from '@angular/router';

export interface Position {
  symbol: string;
  sharesFraction: number;
  returnPercent: number;
}

@Component({
  selector: 'app-positions-list',
  imports: [RouterLink],
  templateUrl: './positions-list.html',
  styleUrl: './positions-list.css',
})
export class PositionsList {
  @Input({ required: true }) positions!: Position[];
  @Input() authorized = true;

  private router = inject(Router);

  navigateTo(symbol: string) {
    if (!this.authorized) return;
    this.router.navigate(['/analysis', symbol]);
  }
}
