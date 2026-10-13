import { ChangeDetectionStrategy, Component, computed, effect, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule } from '@angular/forms';

import { Button } from '../../../components/button/button';
import { AuthStore } from '../../../core/auth/auth.store';
import { ProfileStore } from '../../../core/profile/profile.store';

/** Same limits the API enforces. */
const MAX_NAME = 200;
const MAX_WEIGHT = 500;

@Component({
  selector: 'Nimbus-profile',
  imports: [ReactiveFormsModule, Button],
  templateUrl: './profile.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './profile.css',
})
export default class Profile {
  private readonly store = inject(ProfileStore);
  private readonly fb = inject(NonNullableFormBuilder);

  protected readonly email = inject(AuthStore).email;
  protected readonly profile = this.store.profile;
  protected readonly saving = this.store.saving;
  protected readonly formError = this.store.saveError;
  protected readonly serverErrors = this.store.fieldErrors;

  protected readonly savedNotice = signal(false);
  protected readonly submitted = signal(false);

  protected readonly form = this.fb.group({
    operatorName: '',
    picWeightKg: '',
  });

  private readonly values = toSignal(this.form.valueChanges, { initialValue: this.form.getRawValue() });

  /** Both fields are optional; only filled-in values are validated. */
  protected readonly errors = computed(() => {
    const v = this.values();
    const out: { operatorName?: string; picWeightKg?: string } = {};
    if ((v.operatorName ?? '').trim().length > MAX_NAME) out.operatorName = `Enter at most ${MAX_NAME} characters.`;
    const raw = String(v.picWeightKg ?? '').trim();
    if (raw !== '') {
      const w = Number(raw);
      if (!(w > 0)) out.picWeightKg = 'Enter a weight above 0 kg.';
      else if (w > MAX_WEIGHT) out.picWeightKg = `Enter a weight of at most ${MAX_WEIGHT} kg.`;
      else if (Math.round(w * 100) / 100 !== w) out.picWeightKg = 'Use at most 2 decimal places.';
    }
    return out;
  });
  protected readonly valid = computed(() => Object.keys(this.errors()).length === 0);

  constructor() {
    // Whenever the server value changes (load, save) the form shows it.
    effect(() => {
      if (!this.profile.hasValue()) return;
      const p = this.profile.value();
      this.form.reset({
        operatorName: p.operatorName ?? '',
        picWeightKg: p.picWeightKg === null ? '' : String(Number(p.picWeightKg)),
      });
      this.submitted.set(false);
    });
  }

  protected touchedError(field: 'operatorName' | 'picWeightKg'): string | undefined {
    const msg = this.errors()[field] ?? this.serverErrors()[field];
    return msg && (this.form.controls[field].dirty || this.submitted() || this.serverErrors()[field]) ? msg : undefined;
  }

  protected onEdit(): void {
    this.savedNotice.set(false);
    this.store.clearErrors();
  }

  protected async save(): Promise<void> {
    this.submitted.set(true);
    if (!this.valid() || this.saving()) return;
    const raw = this.form.getRawValue();
    const name = raw.operatorName.trim();
    const weight = String(raw.picWeightKg ?? '').trim();
    this.savedNotice.set(false);
    const ok = await this.store.save({ operatorName: name === '' ? null : name, picWeightKg: weight === '' ? null : Number(weight) });
    if (ok) this.savedNotice.set(true);
  }
}
