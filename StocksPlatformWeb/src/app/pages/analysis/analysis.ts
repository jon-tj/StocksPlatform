import { Component, OnInit, inject } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';

@Component({
  selector: 'app-analysis',
  imports: [RouterLink],
  templateUrl: './analysis.html',
  styleUrl: './analysis.css',
})
export class Analysis implements OnInit {
  private route = inject(ActivatedRoute);
  asset: string | null = null;

  ngOnInit() {
    this.asset = this.route.snapshot.paramMap.get('asset');
  }
}
