import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';

import { Button } from '../../../components/button/button';
import { EmptyState } from '../../../components/empty-state/empty-state';
import { Modal } from '../../../components/modal/modal';
import { type PilotDto } from '../../../core/api-client';
import { PilotsStore } from '../../../core/pilots/pilots.store';

/** Same plausible range the API enforces (kg). */
const MIN_WEIGHT = 20;
const MAX_WEIGHT = 250;

type FormMode = null | 'new' | string; // null = list, 'new' = adding, otherwise the id being edited

@Component({
  selector: 'Nimbus-pilots',
  imports: [ReactiveFormsModule, Button, EmptyState, Modal],
  templateUrl: './pilots.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './pilots.css',
})
export default class Pilots {
  private readonly store = inject(PilotsStore);
  private readonly fb = inject(NonNullableFormBuilder);

  protected readonly pilots = this.store.pilots;
  protected readonly list = this.store.list;
  protected readonly saving = this.store.saving;
  protected readonly formError = this.store.saveError;
  protected readonly listError = this.store.listError;
  protected readonly deleting = this.store.deleting;

  protected readonly mode = signal<FormMode>(null);
  protected readonly submitted = signal(false);
  protected readonly toDelete = signal<PilotDto | null>(null);

  protected readonly form = this.fb.group({
    name: ['', [Validators.required, Validators.maxLength(100), Validators.pattern(/\S/)]],
    weightKg: [0, [Validators.required, Validators.min(MIN_WEIGHT), Validators.max(MAX_WEIGHT)]],
  });

  protected readonly weightHint = `Enter a weight between ${MIN_WEIGHT} and ${MAX_WEIGHT} kg.`;

  protected readonly title = computed(() => {
    const id = this.mode();
    if (id === null || id === 'new') return 'Add pilot';
    return `Edit ${this.list().find((p) => p.id === id)?.name ?? 'pilot'}`;
  });

  protected readonly confirmOpen = computed(() => this.toDelete() !== null);
  protected readonly confirmTitle = computed(() => `Delete ${this.toDelete()?.name ?? ''}?`);
  protected readonly confirmText = computed(
    () => `${this.toDelete()?.name ?? 'This pilot'} will be removed from your pilots. This can't be undone.`,
  );

  protected num(value: number | string): number {
    return Number(value);
  }

  protected add(): void {
    this.open('new', { name: '', weightKg: 0 });
  }

  protected edit(p: PilotDto): void {
    this.open(p.id, { name: p.name, weightKg: Number(p.weightKg) });
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
    const { name, weightKg } = this.form.getRawValue();
    if (await this.store.save(mode === 'new' ? null : mode, { name: name.trim(), weightKg: Number(weightKg) })) {
      this.mode.set(null);
    }
  }

  protected askDelete(p: PilotDto): void {
    this.toDelete.set(p);
  }

  protected closeDialog(): void {
    if (this.deleting()) return;
    this.toDelete.set(null);
  }

  protected async confirmDelete(): Promise<void> {
    const p = this.toDelete();
    if (!p) return;
    await this.store.remove(p.id); // a failure shows up as the list error
    this.toDelete.set(null);
  }
}
