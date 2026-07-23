import { Component, OnInit, signal, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { CommonModule } from '@angular/common';
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
@Component({
    selector: 'app-landing',
    standalone: true,
    imports: [CommonModule, RouterModule],
    templateUrl: './landing.component.html'
})
export class LandingComponent implements OnInit {
    private readonly http = inject(HttpClient);
    private readonly apiUrl = environment.apiUrl;
    protected readonly currentUser = signal<PlayerDto | null>(null);
    protected readonly isLoadingUser = signal(true);
    protected readonly isMenuOpen = signal(false); // Added for mobile navigation toggle
    ngOnInit() {
        this.fetchCurrentUser();
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
    login() {
        window.location.href = `${this.apiUrl}/auth/login-discord`;
    }
    logout() {
        this.http.post(`${this.apiUrl}/auth/logout`, {}, { withCredentials: true }).subscribe({
            next: () => {
                this.currentUser.set(null);
            },
            error: (err) => {
                console.error('Failed to logout', err);
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
}