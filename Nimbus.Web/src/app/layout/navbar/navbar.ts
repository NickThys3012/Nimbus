import { Component, inject } from '@angular/core';
import { ThemeService } from '../../services/theme-service';
import { CommonModule, NgOptimizedImage } from '@angular/common';
import { RouterModule } from '@angular/router';

@Component({
  selector: 'Nimbus-navbar',
  imports: [RouterModule, CommonModule, NgOptimizedImage],
  templateUrl: './navbar.html',
  styleUrl: './navbar.css',
})
export class Navbar {
  readonly themeService = inject(ThemeService);

  // TODO: add a property to the navItems array to indicate if the item is admin only, and then use that property to conditionally render the item in the template based on the user's role.
  navItems = [
    { label: 'Flights', icon: 'ti-list', route: '/flights' },
    { label: 'Logbook', icon: 'ti-chart-bar', route: '/logbook' },
    { label: 'Settings', icon: 'ti-settings', route: '/settings' },
    { label: 'Manual', icon: 'ti-help', route: '/manual' },
    { label: 'Admin', icon: 'ti-shield-check', route: '/admin' },
  ];

  userInitials = 'NT'; // Should come from auth service
}