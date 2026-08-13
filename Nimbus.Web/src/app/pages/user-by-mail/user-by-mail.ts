import { Component, inject } from '@angular/core';
import { AuthStore } from '../../core/auth/auth.store';

@Component({
  selector: 'Nimbus-user-by-mail',
  imports: [],
  templateUrl: './user-by-mail.html',
  styleUrl: './user-by-mail.css',
})
export default class UserByMail {
  protected readonly authStore = inject(AuthStore);
}
