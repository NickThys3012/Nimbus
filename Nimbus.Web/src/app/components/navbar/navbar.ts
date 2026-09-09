import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { ThemeService } from '../../services/theme-service';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { Logo } from '../logo/logo';
import { Avatar } from './components/avatar/avatar';
import { AuthStore } from '../../core/auth/auth.store';
import { Button } from '../button/button';
import { MobileMenu } from './components/mobile-menu/mobile-menu';
import { IconButton } from './components/icon-button/icon-button';
import { NavItem } from './models/nav-item';

@Component({
  selector: 'Nimbus-navbar',
  imports: [RouterModule, CommonModule, Logo, Avatar, Button, MobileMenu, IconButton],
  templateUrl: './navbar.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './navbar.css',
})
export class Navbar {
  readonly themeService = inject(ThemeService);
  readonly authStore = inject(AuthStore);
  readonly router = inject(Router); // Should come from auth service
  protected readonly menuOpen = signal<boolean>(false);
  private readonly navItems: NavItem[] = [
    new NavItem('Flights', 'ti-list', '/flights', true),
    new NavItem('Settings', 'ti-settings', '/settings', true),
    new NavItem('Manual', 'ti-help', '/manual', true),
    new NavItem('Admin', 'ti-shield-check', '/admin', true, 'Admin'),
    new NavItem('Overview', 'ti-layout-grid', '/Overview', false),
    new NavItem('Preparation', 'ti-clipboard-check', '/preparation', false),
  ];

  readonly visibleNavItems = computed(() => {
    const isLoggedIn = this.authStore.isAuthenticated();
    const role = this.authStore.role();

    return this.navItems.filter((item) => {
      // Show only public items while logged out.
      if (!isLoggedIn) {
        return !item.requiresAuth;
      }

      // Once logged in, hide public items and enforce role-gated items.
      if (!item.requiresAuth) {
        return false;
      }

      return !item.requiresRole || item.requiresRole === role;
    });
  });
}
