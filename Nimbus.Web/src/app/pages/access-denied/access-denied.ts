import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'Nimbus-access-denied',
  imports: [RouterLink],
  template: `
    <main class="denied" role="alert">
      <h1>Access denied</h1>
      <p>You don't have permission to view this page.</p>
      <a routerLink="/home">Back to home</a>
    </main>
  `,
  styles: `
    .denied { max-width: 420px; margin: 15vh auto; padding: 0 20px; text-align: center; }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export default class AccessDenied {}
