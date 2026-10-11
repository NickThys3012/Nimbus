import { Component, ChangeDetectionStrategy, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { AccountStatus } from './components/account-status/account-status';
import { Navbar } from './components/navbar/navbar';
import { AuthStore } from './core/auth/auth.store';

@Component({
  selector: 'Nimbus-root',
  imports: [RouterOutlet, Navbar, AccountStatus],
  templateUrl: './Nimbus.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './Nimbus.css',
})
export class Nimbus {
  protected readonly authStore = inject(AuthStore);
}
