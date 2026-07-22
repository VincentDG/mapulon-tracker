import { Component, OnInit, signal, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { environment } from '../../environments/environment';

interface PlayerDto {
    discordId: string;
    discordUsername: string;
    discordAvatarHash: string;
    discordGlobalName: string;
    riotName: string;
    riotTag: string;
    profileIconId: number;
    isOptedIn: boolean;
    gamesPlayed: number;
    avp: number;
    currentTier: string;
    currentRank: string;
    currentLP: number;
    isAdmin: boolean;
}

interface LeaderboardEntry {
    discordUsername: string;
    discordGlobalName: string;
    discordAvatarHash: string;
    discordId: string;
    riotName: string;
    riotTag: string;
    profileIconId: number;
    gamesPlayed: number;
    avp: number;
    currentTier: string;
    currentRank: string;
    currentLP: number;
}

@Component({
    selector: 'app-tournaments',
    standalone: true,
    imports: [CommonModule, FormsModule, RouterModule],
    templateUrl: './tournaments.component.html'
})
export class TournamentsComponent implements OnInit {
    private readonly http = inject(HttpClient);
    private readonly apiUrl = environment.apiUrl;

    protected readonly currentUser = signal<PlayerDto | null>(null);
    protected readonly leaderboard = signal<LeaderboardEntry[]>([]);
    protected readonly isLoadingUser = signal(true);
    protected readonly isLoadingLeaderboard = signal(true);
    protected readonly isSubmitting = signal(false);

    // Interactive Layout Signals
    protected readonly activeTab = signal<'details' | 'leaderboard'>('details');
    protected readonly showInstructions = signal(true);
    protected readonly showUpdateForm = signal(false);

    protected riotName = '';
    protected riotTag = '';

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

    optIn() {
        this.isSubmitting.set(true);
        this.errorMessage.set(null);
        this.successMessage.set(null);

        this.http.post<{ message: string }>(`${this.apiUrl}/auth/opt-in`, {}, { withCredentials: true }).subscribe({
            next: (res) => {
                this.isSubmitting.set(false);
                this.successMessage.set(res.message || 'Successfully registered for the tournament!');
                this.fetchCurrentUser();
                this.fetchLeaderboard();
                setTimeout(() => this.successMessage.set(null), 5000);
            },
            error: (err) => {
                this.isSubmitting.set(false);
                this.errorMessage.set(err.error || 'Failed to register.');
            }
        });
    }

    optOut() {
        this.isSubmitting.set(true);
        this.errorMessage.set(null);
        this.successMessage.set(null);
        this.http.post<{ message: string }>(`${this.apiUrl}/auth/opt-out`, {}, { withCredentials: true }).subscribe({
            next: (res) => {
                this.isSubmitting.set(false);

                // Locally update currentUser state to immediately reflect deregistration and reset stats in the UI
                const current = this.currentUser();
                if (current) {
                    this.currentUser.set({
                        ...current,
                        isOptedIn: false,
                        gamesPlayed: 0,
                        avp: 0
                    });
                }

                // Fetch actual server updates in the background
                this.fetchCurrentUser();
                this.fetchLeaderboard();
            },
            error: (err) => {
                this.isSubmitting.set(false);
                this.errorMessage.set(err.error || 'Failed to deregister.');
            }
        });
    }

    toggleRegistration() {
        const user = this.currentUser();
        if (!user) return;
        if (user.isOptedIn) {
            this.optOut();
        } else {
            this.optIn();
        }
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
                this.successMessage.set('Riot ID linked successfully! Syncing summoner data...');
                this.riotName = '';
                this.riotTag = '';
                this.showUpdateForm.set(false);
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

    getDiscordAvatar(discordId: string, avatarHash: string): string {
        if (!avatarHash) {
            const index = (BigInt(discordId) % 5n).toString();
            return `https://cdn.discordapp.com/embed/avatars/${index}.png`;
        }
        return `https://cdn.discordapp.com/avatars/${discordId}/${avatarHash}.png`;
    }

    getRiotSummonerIcon(iconId: number): string {
        if (!iconId) {
            return 'https://ddragon.leagueoflegends.com/cdn/14.13.1/img/profileicon/29.png';
        }
        return `https://ddragon.leagueoflegends.com/cdn/14.13.1/img/profileicon/${iconId}.png`;
    }
}
