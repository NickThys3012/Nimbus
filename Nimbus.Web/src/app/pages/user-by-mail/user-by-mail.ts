import { Component, inject, ChangeDetectionStrategy } from '@angular/core';
import { AuthStore } from '../../core/auth/auth.store';

@Component({
  selector: 'Nimbus-user-by-mail',
  imports: [],
  templateUrl: './user-by-mail.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrl: './user-by-mail.css',
})
export default class UserByMail {
  protected readonly authStore = inject(AuthStore);
}
