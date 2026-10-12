import { ChangeDetectionStrategy, Component, ElementRef, computed, effect, inject, signal } from '@angular/core';
import { rxResource, toSignal } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { map, startWith } from 'rxjs';

import { Button } from '../../../components/button/button';
import { EmptyState } from '../../../components/empty-state/empty-state';
import {
  ApiError,
  type BalloonDto,
  type BalloonRequestDto,
  type BalloonUsageDto,
  BalloonsService,
} from '../../../core/api-client';

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
  imports: [ReactiveFormsModule, Button, EmptyState],
  templateUrl: './balloons.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './balloons.css',
  host: { '(document:keydown.escape)': 'closeDialog()' },
})
export default class Balloons {
  private readonly api = inject(BalloonsService);
  private readonly fb = inject(NonNullableFormBuilder);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);

  protected readonly balloons = rxResource({ stream: () => this.api.getApiBalloons() });
  protected readonly list = computed(() => (this.balloons.hasValue() ? this.balloons.value() : []));

  protected readonly mode = signal<FormMode>(null);
  protected readonly saving = signal(false);
  protected readonly submitted = signal(false);
  protected readonly duplicate = signal(false);
  protected readonly formError = signal<string | null>(null);
  protected readonly listError = signal<string | null>(null);

  protected readonly dialog = signal<DeleteDialog | null>(null);
  protected readonly deleting = signal(false);
  /** The row whose usage is being looked up (delete clicked, dialog not open yet). */
  protected readonly checkingId = signal<string | null>(null);
  private opener: HTMLElement | null = null;

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

  constructor() {
    // Move focus into a dialog when it opens, and give it back to the trigger when it closes.
    effect(() => {
      if (this.dialog()) {
        queueMicrotask(() => this.host.nativeElement.querySelector<HTMLElement>('[data-autofocus] button')?.focus());
      } else if (this.opener) {
        this.opener.focus();
        this.opener = null;
      }
    });
  }

  protected num(value: number | string): number {
    return Number(value);
  }

  protected shownFlights(usage: BalloonUsageDto) {
    return usage.recentFlights.slice(0, FLIGHTS_SHOWN);
  }

  protected moreFlights(usage: BalloonUsageDto): number {
    return Math.max(0, Number(usage.flightCount) - Math.min(usage.recentFlights.length, FLIGHTS_SHOWN));
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
    this.formError.set(null);
    this.duplicate.set(false);
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

  protected save(): void {
    this.submitted.set(true);
    const mode = this.mode();
    if (this.form.invalid || mode === null || this.saving()) return;
    this.saving.set(true);
    this.formError.set(null);
    this.duplicate.set(false);
    const requestBody: BalloonRequestDto = this.form.getRawValue();
    const call =
      mode === 'new' ? this.api.postApiBalloons({ requestBody }) : this.api.putApiBalloons({ id: mode, requestBody });
    call.subscribe({
      next: () => {
        this.saving.set(false);
        this.mode.set(null);
        this.balloons.reload();
      },
      error: (err: unknown) => {
        this.saving.set(false);
        if (err instanceof ApiError && err.status === 409) this.duplicate.set(true);
        else this.formError.set(this.describe(err));
      },
    });
  }

  /** Looks up the flights using the balloon first: used balloons get the "can't be deleted" dialog. */
  protected askDelete(balloon: BalloonDto, event: Event): void {
    if (this.checkingId()) return;
    this.opener = event.currentTarget as HTMLElement | null;
    this.checkingId.set(balloon.id);
    this.listError.set(null);
    this.api.getApiBalloonsUsage({ id: balloon.id }).subscribe({
      next: (usage) => {
        this.checkingId.set(null);
        this.dialog.set(Number(usage.flightCount) > 0 ? { kind: 'blocked', balloon, usage } : { kind: 'confirm', balloon });
      },
      error: (err: unknown) => {
        this.checkingId.set(null);
        this.listError.set(this.describe(err));
      },
    });
  }

  protected closeDialog(): void {
    if (this.deleting()) return;
    this.dialog.set(null);
  }

  protected confirmDelete(): void {
    const d = this.dialog();
    if (d?.kind !== 'confirm' || this.deleting()) return;
    this.deleting.set(true);
    this.api.deleteApiBalloons({ id: d.balloon.id }).subscribe({
      next: () => {
        this.deleting.set(false);
        this.dialog.set(null);
        this.balloons.reload();
      },
      error: (err: unknown) => {
        this.deleting.set(false);
        this.dialog.set(null);
        // e.g. 409: a flight started using it meanwhile; the server message explains why.
        this.listError.set(this.describe(err));
        this.balloons.reload();
      },
    });
  }

  protected onBackdrop(event: MouseEvent): void {
    if (event.target === event.currentTarget) this.closeDialog();
  }

  private describe(err: unknown): string {
    if (err instanceof ApiError) {
      const body = err.body as { title?: string; errors?: Record<string, string[]> } | undefined;
      if (err.status === 422 && body?.errors) return Object.values(body.errors).flat().join(' ');
      if (typeof body?.title === 'string') return body.title;
    }
    return 'Something went wrong. Please try again.';
  }

  protected readonly duplicateMessage = DUPLICATE_MESSAGE;
}
