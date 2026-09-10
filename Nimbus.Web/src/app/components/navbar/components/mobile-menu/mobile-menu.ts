import { Component, inject, input, Input, WritableSignal } from '@angular/core';
import { Button } from '../../../button/button';
import { IconButton } from '../icon-button/icon-button';
import { NavItem } from '../../models/nav-item';
import { Router, RouterLink, RouterLinkActive } from '@angular/router';

@Component({
  imports: [Button, IconButton, RouterLinkActive, RouterLink],
  selector: 'Nimbus-mobile-menu',
  styleUrl: './mobile-menu.css',
  templateUrl: './mobile-menu.html',
})
export class MobileMenu {
  @Input() menuOpen!: WritableSignal<boolean>;
  navItems = input<NavItem[]>([]);

  readonly router = inject(Router);

  closeMenu(): void {
    this.menuOpen.set(false);
  }

  navigateTo(commands: string[]): void {
    this.closeMenu();
    void this.router.navigate(commands);
  }
}
