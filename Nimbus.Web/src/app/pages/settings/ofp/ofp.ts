import { ChangeDetectionStrategy, Component, computed, effect, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule } from '@angular/forms';

import { Button } from '../../../components/button/button';
import { Modal } from '../../../components/modal/modal';
import { OfpSettingsStore } from '../../../core/ofp-settings/ofp-settings.store';

/** Built-in default (also what the reset dialog announces). */
export const DEFAULT_KG = 7;
/** Same range the API enforces. */
const MAX_KG = 100;
/** Passengers in the worked example. */
const EXAMPLE_PASSENGERS = 8;

@Component({
  selector: 'Nimbus-ofp',
  imports: [ReactiveFormsModule, Button, Modal],
  templateUrl: './ofp.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './ofp.css',
})
export default class Ofp {
  private readonly store = inject(OfpSettingsStore);
  private readonly fb = inject(NonNullableFormBuilder);

  protected readonly settings = this.store.settings;
  protected readonly saving = this.store.saving;
  protected readonly resetting = this.store.resetting;
  protected readonly formError = this.store.saveError;
  protected readonly serverError = this.store.fieldError;

  protected readonly defaultKg = DEFAULT_KG;
  protected readonly passengers = EXAMPLE_PASSENGERS;
  protected readonly resetOpen = signal(false);
  protected readonly savedNotice = signal(false);
  protected readonly submitted = signal(false);

  protected readonly form = this.fb.group({ weight: [DEFAULT_KG as number | null] });
  private readonly values = toSignal(this.form.valueChanges, { initialValue: this.form.getRawValue() });

  /** Saved value from the server, as a number. */
  protected readonly savedKg = computed(() => (this.settings.hasValue() ? Number(this.settings.value().equipmentWeightPerPassengerKg) : DEFAULT_KG));
  protected readonly isCustom = computed(() => this.settings.hasValue() && this.settings.value().isCustom);

  /** The typed weight, or null when empty / not a number. */
  private readonly typedKg = computed(() => {
    const raw = this.values().weight;
    return raw === null || raw === undefined || (raw as unknown) === '' || Number.isNaN(Number(raw)) ? null : Number(raw);
  });

  protected readonly error = computed(() => {
    const v = this.typedKg();
    if (v === null) return 'Enter a weight of 0 kg or more.';
    if (v < 0) return 'Enter a weight of 0 kg or more.';
    if (v > MAX_KG) return `Enter a weight of ${MAX_KG} kg or less.`;
    if (Math.round(v * 100) / 100 !== v) return 'Use at most 2 decimal places.';
    return undefined;
  });
  protected readonly valid = computed(() => this.error() === undefined);
  protected readonly changed = computed(() => this.valid() && this.typedKg() !== this.savedKg());

  protected readonly typedKgText = computed(() => String(this.typedKg() ?? ''));

  protected readonly exampleKg = computed(() => (this.valid() ? this.passengers * (this.typedKg() as number) : null));
  protected readonly exampleWasKg = computed(() => this.passengers * this.savedKg());

  constructor() {
    // Whenever the server value changes (load, save, reset) the form shows it.
    effect(() => {
      if (!this.settings.hasValue()) return;
      this.form.reset({ weight: Number(this.settings.value().equipmentWeightPerPassengerKg) });
      this.submitted.set(false);
    });
  }

  protected touchedError(): string | undefined {
    const msg = this.error() ?? this.serverError() ?? undefined;
    return msg && (this.form.controls.weight.dirty || this.submitted() || this.serverError()) ? msg : undefined;
  }

  protected onEdit(): void {
    this.savedNotice.set(false);
    this.store.clearErrors();
  }

  protected async save(): Promise<void> {
    this.submitted.set(true);
    if (!this.valid() || this.saving()) return;
    this.savedNotice.set(false);
    if (await this.store.save({ equipmentWeightPerPassengerKg: this.typedKg() as number })) this.savedNotice.set(true);
  }

  protected askReset(): void {
    this.resetOpen.set(true);
  }

  protected closeReset(): void {
    if (this.resetting()) return;
    this.resetOpen.set(false);
  }

  protected async confirmReset(): Promise<void> {
    this.savedNotice.set(false);
    await this.store.reset(); // a failure shows up as the form error
    this.resetOpen.set(false);
  }
}
