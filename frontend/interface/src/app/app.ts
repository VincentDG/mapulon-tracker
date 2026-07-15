import { Component, OnInit, signal, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { RouterOutlet } from '@angular/router';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { environment } from '../environments/environment';

interface PlayerDto {
  discordId: string;
  discordUsername: string;
  riotName: string;
  riotTag: string;
  currentTier: string;
  currentRank: string;
  currentLP: number;
}

interface LeaderboardEntry {
  discordUsername: string;
  riotName: string;
  riotTag: string;
  rankIcon: string;
  currentTier: string;
  currentRank: string;
  currentLP: number;
}

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './app.html',
  styleUrl: './app.css'
})
export class App implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = environment.apiUrl; // Resolve URL dynamically based on compilation profiles

  // Signals for state management
  protected readonly currentUser = signal<PlayerDto | null>(null);
  protected readonly leaderboard = signal<LeaderboardEntry[]>([]);
  protected readonly isLoadingUser = signal(true);
  protected readonly isLoadingLeaderboard = signal(true);
  protected readonly isSubmitting = signal(false);

  // Form Models
  protected riotName = '';
  protected riotTag = '';

  // User feedback states
  protected errorMessage = signal<string | null>(null);
  protected successMessage = signal<string | null>(null);

  ngOnInit() {
    this.fetchCurrentUser();
    this.fetchLeaderboard();
  }

  fetchCurrentUser() {
    this.isLoadingUser.set(true);
    this.http.get<PlayerDto>(`${this.apiUrl}/auth/me`, { withCredentials: true }).subscribe({
      next: (user) => {
        this.currentUser.set(user);
        this.isLoadingUser.set(false);
      },
      error: () => {
        this.currentUser.set(null);
        this.isLoadingUser.set(false);
      }
    });
  }

  fetchLeaderboard() {
    this.isLoadingLeaderboard.set(true);
    this.http.get<LeaderboardEntry[]>(`${this.apiUrl}/players`).subscribe({
      next: (data) => {
        this.leaderboard.set(data);
        this.isLoadingLeaderboard.set(false);
      },
      error: (err) => {
        console.error('Failed to load leaderboard', err);
        this.isLoadingLeaderboard.set(false);
      }
    });
  }

  login() {
    window.location.href = `${this.apiUrl}/auth/login-discord`;
  }

  logout() {
    this.http.post(`${this.apiUrl}/auth/logout`, {}, { withCredentials: true }).subscribe({
      next: () => {
        this.currentUser.set(null);
        this.successMessage.set('Logged out successfully.');
        setTimeout(() => this.successMessage.set(null), 3000);
      },
      error: (err) => {
        console.error('Failed to logout', err);
      }
    });
  }

  linkRiotId() {
    if (!this.riotName || !this.riotTag) {
      this.errorMessage.set('Riot Name and Tag Line are required.');
      return;
    }

    const user = this.currentUser();
    if (!user) {
      this.errorMessage.set('You must be logged in to link a Riot ID.');
      return;
    }

    this.isSubmitting.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);

    const payload = {
      discordId: user.discordId,
      riotName: this.riotName.trim(),
      riotTag: this.riotTag.trim()
    };

    this.http.post<{ success: boolean, message: string }>(`${this.apiUrl}/players/link-riot`, payload, { withCredentials: true }).subscribe({
      next: () => {
        this.isSubmitting.set(false);
        this.successMessage.set('Riot ID linked successfully! Refreshing leaderboard...');
        this.riotName = '';
        this.riotTag = '';
        this.fetchCurrentUser();
        this.fetchLeaderboard();
        setTimeout(() => this.successMessage.set(null), 5000);
      },
      error: (err) => {
        this.isSubmitting.set(false);
        this.errorMessage.set(err.error?.message || 'Failed to link Riot ID.');
      }
    });
  }
}

