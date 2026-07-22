import { Routes } from '@angular/router';
import { LandingComponent } from './landing/landing.component';
import { TournamentsComponent } from './tournaments/tournaments.component';
export const routes: Routes = [
    { path: '', component: LandingComponent, pathMatch: 'full' }, // Added pathMatch: 'full'
    { path: 'tournaments', component: TournamentsComponent },
    { path: '**', redirectTo: '' }
];