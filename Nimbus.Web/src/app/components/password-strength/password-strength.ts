import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

const LABELS = ['—', 'weak', 'fair', 'good', 'strong'];

/** Strength bars + rule checklist, as in the auth-states mockup. Mirrors the server's password rules. */
@Component({
  selector: 'Nimbus-password-strength',
  templateUrl: './password-strength.html',
  styleUrl: './password-strength.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PasswordStrength {
  value = input<string>('');

  score = computed(() => {
    const v = this.value();
    if (!v) return 0;
    let s = 0;
    if (v.length >= 8) s++;
    if (v.length >= 12) s++;
    if (/\d/.test(v) && /[a-z]/i.test(v)) s++;
    if (/[^A-Za-z0-9]/.test(v)) s++;
    return Math.min(s, 4);
  });
  label = computed(() => LABELS[this.score()]);
  color = computed(() => (this.score() <= 1 ? 'var(--r)' : this.score() === 2 ? 'var(--a)' : 'var(--g)'));
  bars = [0, 1, 2, 3];
  rules = computed(() => {
    const v = this.value();
    return [
      { text: 'At least 8 characters', met: v.length >= 8 },
      { text: 'At least one digit', met: /\d/.test(v) },
      { text: 'Upper and lower case letter', met: /[a-z]/.test(v) && /[A-Z]/.test(v) },
    ];
  });
}
