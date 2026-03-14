import { Component, Input, inject } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { Position } from '../../services/positions.service';
import { DecimalPipe } from '@angular/common';

@Component({
  selector: 'app-positions-list',
  imports: [RouterLink, DecimalPipe],
  templateUrl: './positions-list.html',
  styleUrl: './positions-list.css',
})
export class PositionsList {
  @Input({ required: true }) positions!: Position[];
  @Input() authorized = true;
  @Input() numRows = 4;
  @Input() filter = '';

  get filteredPositions(): Position[] {
    const q = this.filter.trim().toLowerCase();
    const source = this.authorized ? this.positions : this.positions.slice(0, this.numRows);
    if (!q) return source;
    return source.filter(p =>
      p.symbol?.toLowerCase().includes(q) || p.name?.toLowerCase().includes(q)
    );
  }

  private router = inject(Router);

  navigateTo(symbol: string) {
    if (!this.authorized) return;
    this.router.navigate(['/analysis', symbol]);
  }
}
