import { Component, inject, OnInit } from '@angular/core';
import { Router, RouterLink, RouterOutlet } from '@angular/router';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-layout',
  imports: [RouterOutlet, RouterLink],
  templateUrl: './layout.html',
  styleUrl: './layout.css',
})
export class AppLayout implements OnInit {
  private auth = inject(AuthService);
  private router = inject(Router);

  displayName = '';

  ngOnInit() {
    this.displayName = this.auth.getUser()?.displayName ?? '';
  }

  logout() {
    this.auth.logout();
    this.router.navigate(['/login']);
  }
}
