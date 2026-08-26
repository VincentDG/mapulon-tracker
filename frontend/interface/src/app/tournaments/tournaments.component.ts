import { Component, OnInit, signal, inject, computed } from '@angular/core';
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
    isRegistrationOpen: boolean; // Added status field
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
    hasHitMasterThisSet: boolean;
    timestampHitMaster?: Date | null;
    avpOnHitMaster?: number | null;
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
    protected readonly isLoadingUser = signal(true);
    protected readonly leaderboard = signal<LeaderboardEntry[]>([]);
    protected readonly isLoadingLeaderboard = signal(true);
    protected readonly activeTab = signal<'details' | 'leaderboard'>('details');
    protected readonly showUpdateForm = signal(false);
    protected readonly isMenuOpen = signal(false);
    protected readonly isRegistrationOpen = signal(true);
    protected readonly isSubmitting = signal(false);
    // Sorting Signals
    protected readonly sortColumn = signal<'rank' | 'games' | 'avp' | 'masterTime'>('rank');
    protected readonly sortDirection = signal<'asc' | 'desc'>('desc');
    // Reactive Sorted Leaderboard
    protected readonly sortedLeaderboard = computed(() => {
        const list = [...this.leaderboard()];
        const col = this.sortColumn();
        const dir = this.sortDirection();
        const tierOrder: Record<string, number> = {
            'CHALLENGER': 9, 'GRANDMASTER': 8, 'MASTER': 7,
            'DIAMOND': 6, 'EMERALD': 5, 'PLATINUM': 4,
            'GOLD': 3, 'SILVER': 2, 'BRONZE': 1, 'IRON': 0, 'UNRANKED': -1
        };
        const divOrder: Record<string, number> = { 'I': 4, 'II': 3, 'III': 2, 'IV': 1 };
        return list.sort((a, b) => {
            let res = 0;
            if (col === 'rank') {
                const scoreA = (tierOrder[a.currentTier?.toUpperCase()] ?? -1) * 10000 + (divOrder[a.currentRank?.toUpperCase()] ?? 0) * 1000 + (a.currentLP || 0);
                const scoreB = (tierOrder[b.currentTier?.toUpperCase()] ?? -1) * 10000 + (divOrder[b.currentRank?.toUpperCase()] ?? 0) * 1000 + (b.currentLP || 0);
                res = scoreA - scoreB;
            } else if (col === 'games') {
                res = a.gamesPlayed - b.gamesPlayed;
            } else if (col === 'avp') {
                const avpA = a.hasHitMasterThisSet && a.avpOnHitMaster != null ? a.avpOnHitMaster : (a.gamesPlayed > 0 ? a.avp : 999);
                const avpB = b.hasHitMasterThisSet && b.avpOnHitMaster != null ? b.avpOnHitMaster : (b.gamesPlayed > 0 ? b.avp : 999);
                res = avpA - avpB;
            } else if (col === 'masterTime') {
                const timeA = a.hasHitMasterThisSet && a.timestampHitMaster ? new Date(a.timestampHitMaster).getTime() : Number.MAX_SAFE_INTEGER;
                const timeB = b.hasHitMasterThisSet && b.timestampHitMaster ? new Date(b.timestampHitMaster).getTime() : Number.MAX_SAFE_INTEGER;
                res = timeA - timeB;
            }
            return dir === 'asc' ? res : -res;
        });
    });
    toggleSort(column: 'rank' | 'games' | 'avp' | 'masterTime') {
        if (this.sortColumn() === column) {
            this.sortDirection.update(d => d === 'asc' ? 'desc' : 'asc');
        } else {
            this.sortColumn.set(column);
            // Default logical directions on first click (lowest AVP and earliest Master is best)
            this.sortDirection.set(column === 'avp' || column === 'masterTime' ? 'asc' : 'desc');
        }
    }
    // Interactive Layout Signals
    protected readonly showInstructions = signal(true);

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
                this.isRegistrationOpen.set(user.isRegistrationOpen);
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
