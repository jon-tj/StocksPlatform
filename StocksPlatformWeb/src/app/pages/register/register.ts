import { Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-register',
  imports: [FormsModule, RouterLink],
  templateUrl: './register.html',
  styleUrl: './register.css',
})
export class Register {
  private auth = inject(AuthService);
  private router = inject(Router);

  displayName = '';
  email = '';
  password = '';
  loading = false;
  error = '';

  get canSubmit() {
    return this.displayName.trim() && this.email && this.password.length >= 8;
  }

  submit() {
    if (!this.canSubmit) return;
    this.loading = true;
    this.error = '';
    this.auth.register(this.email, this.password, this.displayName.trim()).subscribe({
      next: () => this.router.navigate(['/dashboard']),
      error: (err) => {
        if (err.status === 409) {
          this.error = 'An account with this email already exists.';
        } else if (err.status === 400 && err.error) {
          const msgs: string[] = Array.isArray(err.error)
            ? err.error.map((e: { description: string }) => e.description)
            : [err.error.message ?? 'Validation failed.'];
          this.error = msgs.join(' ');
        } else {
          this.error = 'Something went wrong. Please try again.';
        }
        this.loading = false;
      },
    });
  }
}
