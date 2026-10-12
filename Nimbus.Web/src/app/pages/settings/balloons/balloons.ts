import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { toSignal } from '@angular/core/rxjs-interop';
import { map, startWith } from 'rxjs';

import { Button } from '../../../components/button/button';
import { EmptyState } from '../../../components/empty-state/empty-state';
import { Modal } from '../../../components/modal/modal';
import { type BalloonDto, type BalloonUsageDto } from '../../../core/api-client';
import { BalloonsStore } from '../../../core/balloons/balloons.store';

/** How many recent flights the "can't be deleted" dialog lists before "+ N more". */
const FLIGHTS_SHOWN = 2;
const MONTHS = ['JAN', 'FEB', 'MAR', 'APR', 'MAY', 'JUN', 'JUL', 'AUG', 'SEP', 'OCT', 'NOV', 'DEC'];
const DUPLICATE_MESSAGE = 'You already have a balloon with this registration.';

type FormMode = null | 'new' | string; // null = list, 'new' = adding, otherwise the id being edited

type DeleteDialog =
  | { kind: 'confirm'; balloon: BalloonDto }
  | { kind: 'blocked'; balloon: BalloonDto; usage: BalloonUsageDto };

@Component({
  selector: 'Nimbus-balloons',
  imports: [ReactiveFormsModule, Button, EmptyState, Modal],
  templateUrl: './balloons.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './balloons.css',
})
export default class Balloons {
  private readonly store = inject(BalloonsStore);
  private readonly fb = inject(NonNullableFormBuilder);

  protected readonly balloons = this.store.balloons;
  protected readonly list = this.store.list;
  protected readonly saving = this.store.saving;
  protected readonly duplicate = this.store.duplicate;
  protected readonly formError = this.store.saveError;
  protected readonly listError = this.store.listError;
  protected readonly checkingId = this.store.checkingId;
  protected readonly deleting = this.store.deleting;

  protected readonly mode = signal<FormMode>(null);
  protected readonly submitted = signal(false);
  protected readonly dialog = signal<DeleteDialog | null>(null);

  protected readonly form = this.fb.group({
    registration: ['', [Validators.required, Validators.maxLength(20), Validators.pattern(/\S/)]],
    type: ['', [Validators.maxLength(100)]],
    envelopeVolumeM3: [0, [Validators.required, Validators.min(1), Validators.max(20000)]],
    maxEnvelopeTemperatureC: [0, [Validators.required, Validators.min(1), Validators.max(250)]],
    envelopeWeightKg: [0, [Validators.required, Validators.min(0), Validators.max(5000)]],
    basketWeightKg: [0, [Validators.required, Validators.min(0), Validators.max(5000)]],
    burnerWeightKg: [0, [Validators.required, Validators.min(0), Validators.max(5000)]],
    cylindersWeightKg: [0, [Validators.required, Validators.min(0), Validators.max(5000)]],
  });

  /** The four reference weights, live. The empty weight is only ever derived from these. */
  protected readonly weights = toSignal(
    this.form.valueChanges.pipe(
      startWith(null),
      map(() => {
        const v = this.form.getRawValue();
        return [v.envelopeWeightKg, v.basketWeightKg, v.burnerWeightKg, v.cylindersWeightKg].map((w) => Number(w) || 0);
      }),
    ),
    { initialValue: [0, 0, 0, 0] },
  );
  protected readonly formula = computed(() => this.weights().join(' + '));
  protected readonly emptyWeight = computed(() => Math.round(this.weights().reduce((a, b) => a + b, 0) * 100) / 100);

  protected readonly title = computed(() => {
    const id = this.mode();
    if (id === null || id === 'new') return 'Add balloon';
    return `Edit ${this.list().find((b) => b.id === id)?.registration ?? 'balloon'}`;
  });

  protected num(value: number | string): number {
    return Number(value);
  }

  protected flightDate(date: string): string {
    // "2026-10-08" -> "08 OCT" (date-only string, so no time zone shift)
    const [, month, day] = date.split('-');
    const name = MONTHS[Number(month) - 1];
    return name ? `${day.slice(0, 2)} ${name}` : '';
  }

  protected add(): void {
    this.open('new', {
      registration: '', type: '', envelopeVolumeM3: 0, maxEnvelopeTemperatureC: 0,
      envelopeWeightKg: 0, basketWeightKg: 0, burnerWeightKg: 0, cylindersWeightKg: 0,
    });
  }

  protected edit(b: BalloonDto): void {
    this.open(b.id, {
      registration: b.registration,
      type: b.type,
      envelopeVolumeM3: Number(b.envelopeVolumeM3),
      maxEnvelopeTemperatureC: Number(b.maxEnvelopeTemperatureC),
      envelopeWeightKg: Number(b.envelopeWeightKg),
      basketWeightKg: Number(b.basketWeightKg),
      burnerWeightKg: Number(b.burnerWeightKg),
      cylindersWeightKg: Number(b.cylindersWeightKg),
    });
  }

  private open(mode: FormMode, value: ReturnType<typeof this.form.getRawValue>): void {
    this.form.reset(value);
    this.store.resetSave();
    this.submitted.set(false);
    this.mode.set(mode);
  }

  protected cancel(): void {
    this.mode.set(null);
  }

  protected invalid(name: keyof typeof this.form.controls): boolean {
    const c = this.form.controls[name];
    return c.invalid && (c.touched || this.submitted());
  }

  protected async save(): Promise<void> {
    this.submitted.set(true);
    const mode = this.mode();
    if (this.form.invalid || mode === null) return;
    if (await this.store.save(mode === 'new' ? null : mode, this.form.getRawValue())) {
      this.mode.set(null);
    }
  }

  /** Looks up the flights using the balloon first: used balloons get the "can't be deleted" dialog. */
  protected async askDelete(balloon: BalloonDto): Promise<void> {
    const usage = await this.store.usage(balloon);
    if (!usage) return;
    this.dialog.set(Number(usage.flightCount) > 0 ? { kind: 'blocked', balloon, usage } : { kind: 'confirm', balloon });
  }

  protected closeDialog(): void {
    if (this.deleting()) return;
    this.dialog.set(null);
  }

  protected async confirmDelete(): Promise<void> {
    const d = this.dialog();
    if (d?.kind !== 'confirm') return;
    await this.store.remove(d.balloon.id); // a refusal (409) shows up as the list error
    this.dialog.set(null);
  }

  protected readonly duplicateMessage = DUPLICATE_MESSAGE;

  // Content of the two shared-modal dialogs (empty while closed).
  private readonly confirming = computed(() => {
    const d = this.dialog();
    return d?.kind === 'confirm' ? d : null;
  });
  private readonly blocked = computed(() => {
    const d = this.dialog();
    return d?.kind === 'blocked' ? d : null;
  });
  protected readonly confirmOpen = computed(() => this.confirming() !== null);
  protected readonly blockedOpen = computed(() => this.blocked() !== null);
  protected readonly confirmTitle = computed(() => `Delete ${this.confirming()?.balloon.registration ?? ''}?`);
  protected readonly confirmText = computed(
    () =>
      `${this.confirming()?.balloon.type || 'This balloon'} will be removed from your balloons. This can't be undone. No flights use this balloon.`,
  );
  protected readonly blockedTitle = computed(() => `${this.blocked()?.balloon.registration ?? ''} can't be deleted`);
  protected readonly blockedText = computed(() => {
    const n = Number(this.blocked()?.usage.flightCount ?? 0);
    return `This balloon is used by ${n} ${n === 1 ? 'flight' : 'flights'}. Deleting it would break ${n === 1 ? 'its' : 'their'} logbook ${n === 1 ? 'entry' : 'entries'}.`;
  });
  protected readonly blockedFlights = computed(() => {
    const usage = this.blocked()?.usage;
    return usage ? usage.recentFlights.slice(0, FLIGHTS_SHOWN) : [];
  });
  protected readonly blockedMore = computed(() => {
    const usage = this.blocked()?.usage;
    return usage ? Math.max(0, Number(usage.flightCount) - Math.min(usage.recentFlights.length, FLIGHTS_SHOWN)) : 0;
  });
}
