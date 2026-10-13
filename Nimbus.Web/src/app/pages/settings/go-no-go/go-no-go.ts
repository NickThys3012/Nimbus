import { ChangeDetectionStrategy, Component, computed, effect, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { toSignal } from '@angular/core/rxjs-interop';

import { Button } from '../../../components/button/button';
import { Modal } from '../../../components/modal/modal';
import { type GoNoGoThresholdsRequestDto } from '../../../core/api-client';
import { GoNoGoStore, type ThresholdField } from '../../../core/go-no-go/go-no-go.store';

/** Built-in defaults (also what the reset dialog announces). */
export const DEFAULTS: GoNoGoThresholdsRequestDto = {
  windYellowKt: 10, windRedKt: 15, visibilityYellowKm: 5, visibilityRedKm: 3, capeYellow: 300, capeRed: 500,
};

/** Sample conditions the preview chip rates against the current form values. */
const PREVIEW = { wind: 12, visibility: 4, cape: 250 };

interface Row {
  id: 'wind' | 'visibility' | 'cape';
  label: string;
  unit: string;
  hint: string;
  yellow: ThresholdField;
  red: ThresholdField;
  /** True when higher is worse (wind, CAPE); false for visibility. */
  higherIsWorse: boolean;
}

const ROWS: Row[] = [
  { id: 'wind', label: 'Surface wind', unit: 'kt', hint: 'NO-GO ABOVE CAUTION', yellow: 'windYellowKt', red: 'windRedKt', higherIsWorse: true },
  { id: 'visibility', label: 'Visibility', unit: 'km', hint: 'NO-GO BELOW CAUTION', yellow: 'visibilityYellowKm', red: 'visibilityRedKm', higherIsWorse: false },
  { id: 'cape', label: 'CAPE', unit: 'J/kg', hint: 'NO-GO ABOVE CAUTION', yellow: 'capeYellow', red: 'capeRed', higherIsWorse: true },
];

/** Same ranges the API enforces. */
const MAX: Record<Row['id'], number> = { wind: 100, visibility: 100, cape: 10000 };

type Level = 'go' | 'caution' | 'nogo';

@Component({
  selector: 'Nimbus-go-no-go',
  imports: [ReactiveFormsModule, Button, Modal],
  templateUrl: './go-no-go.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './go-no-go.css',
})
export default class GoNoGo {
  private readonly store = inject(GoNoGoStore);
  private readonly fb = inject(NonNullableFormBuilder);

  protected readonly thresholds = this.store.thresholds;
  protected readonly saving = this.store.saving;
  protected readonly resetting = this.store.resetting;
  protected readonly formError = this.store.saveError;
  protected readonly serverErrors = this.store.fieldErrors;

  protected readonly rows = ROWS;
  protected readonly defaults = DEFAULTS;
  protected readonly resetOpen = signal(false);
  protected readonly savedNotice = signal(false);
  protected readonly submitted = signal(false);

  protected readonly form = this.fb.group({
    windYellowKt: [Number(DEFAULTS.windYellowKt), Validators.required],
    windRedKt: [Number(DEFAULTS.windRedKt), Validators.required],
    visibilityYellowKm: [Number(DEFAULTS.visibilityYellowKm), Validators.required],
    visibilityRedKm: [Number(DEFAULTS.visibilityRedKm), Validators.required],
    capeYellow: [Number(DEFAULTS.capeYellow), Validators.required],
    capeRed: [Number(DEFAULTS.capeRed), Validators.required],
  });

  private readonly values = toSignal(this.form.valueChanges, { initialValue: this.form.getRawValue() });

  /** Per-field consistency/range message (empty when fine), computed from the live form values. */
  protected readonly errors = computed(() => {
    const v = this.values() as Record<ThresholdField, number | null>;
    const out: Partial<Record<ThresholdField, string>> = {};
    for (const r of ROWS) {
      const y = Number(v[r.yellow]);
      const rd = Number(v[r.red]);
      const max = MAX[r.id];
      if (!(y > 0) || y > max) out[r.yellow] = `Enter a value between 0 and ${max} ${r.unit}.`;
      if (!(rd > 0) || rd > max) out[r.red] = `Enter a value between 0 and ${max} ${r.unit}.`;
      if (out[r.yellow] || out[r.red]) continue;
      if (r.higherIsWorse && rd <= y) out[r.red] = `Must be higher than ${y} ${r.unit}.`;
      if (!r.higherIsWorse && rd >= y) out[r.red] = `Must be lower than ${y} ${r.unit}.`;
    }
    return out;
  });

  protected readonly inconsistentCount = computed(
    () => ROWS.filter((r) => this.errors()[r.red]?.startsWith('Must be')).length,
  );
  protected readonly valid = computed(() => Object.keys(this.errors()).length === 0);

  protected readonly isCustom = computed(() => this.thresholds.hasValue() && this.thresholds.value().isCustom);

  protected readonly preview = computed<{ level: Level; label: string }>(() => {
    const v = this.values() as Record<ThresholdField, number | null>;
    const sample = { wind: PREVIEW.wind, visibility: PREVIEW.visibility, cape: PREVIEW.cape };
    let worst: Level = 'go';
    for (const r of ROWS) {
      const x = sample[r.id];
      const y = Number(v[r.yellow]);
      const rd = Number(v[r.red]);
      const level: Level = (r.higherIsWorse ? x >= rd : x <= rd) ? 'nogo' : (r.higherIsWorse ? x >= y : x <= y) ? 'caution' : 'go';
      if (level === 'nogo' || (level === 'caution' && worst === 'go')) worst = level;
    }
    return { level: worst, label: worst === 'nogo' ? 'NO-GO' : worst === 'caution' ? 'CAU' : 'GO' };
  });

  protected readonly previewText = `PREVIEW · ${PREVIEW.wind} KT · ${PREVIEW.visibility} KM · ${PREVIEW.cape} J/KG`;

  constructor() {
    // Whenever the server value changes (load, save, reset) the form shows it.
    effect(() => {
      if (!this.thresholds.hasValue()) return;
      const t = this.thresholds.value();
      this.form.reset({
        windYellowKt: Number(t.windYellowKt), windRedKt: Number(t.windRedKt),
        visibilityYellowKm: Number(t.visibilityYellowKm), visibilityRedKm: Number(t.visibilityRedKm),
        capeYellow: Number(t.capeYellow), capeRed: Number(t.capeRed),
      });
      this.submitted.set(false);
    });
  }

  protected errorFor(field: ThresholdField): string | undefined {
    return this.errors()[field] ?? this.serverErrors()[field];
  }

  protected touchedError(field: ThresholdField): string | undefined {
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
    const body = Object.fromEntries(Object.entries(raw).map(([k, v]) => [k, Number(v)])) as unknown as GoNoGoThresholdsRequestDto;
    this.savedNotice.set(false);
    if (await this.store.save(body)) this.savedNotice.set(true);
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
