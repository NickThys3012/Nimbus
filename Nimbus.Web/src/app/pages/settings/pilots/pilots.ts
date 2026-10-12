import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';

import { Button } from '../../../components/button/button';
import { EmptyState } from '../../../components/empty-state/empty-state';
import { Modal } from '../../../components/modal/modal';
import { type PilotDto } from '../../../core/api-client';
import { PilotsStore } from '../../../core/pilots/pilots.store';

/** Same plausible range the API enforces (kg). */
const MIN_WEIGHT = 30;
const MAX_WEIGHT = 250;

type FormMode = null | 'new' | string; // null = list, 'new' = adding, otherwise the id being edited

@Component({
  selector: 'Nimbus-pilots',
  imports: [ReactiveFormsModule, Button, EmptyState, Modal],
  templateUrl: './pilots.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './pilots.css',
  host: { '(document:click)': 'onDocumentClick($event)', '(document:keydown.escape)': 'menuId.set(null)' },
})
export default class Pilots {
  private readonly store = inject(PilotsStore);
  private readonly fb = inject(NonNullableFormBuilder);

  protected readonly pilots = this.store.pilots;
  protected readonly list = this.store.list;
  protected readonly saving = this.store.saving;
  protected readonly formError = this.store.saveError;
  protected readonly fieldErrors = this.store.fieldErrors;
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

  protected readonly formOpen = computed(() => this.mode() !== null);
  protected readonly isNew = computed(() => this.mode() === 'new');
  protected readonly title = computed(() => (this.isNew() ? 'Add pilot' : 'Edit pilot'));
  protected readonly subtitle = computed(() => {
    const n = this.list().length;
    return `${n} ${n === 1 ? 'PILOT' : 'PILOTS'} · A–Z · USED IN WEIGHT & BALANCE`;
  });

  /** Id of the pilot whose three-dots menu (mobile) is open. */
  protected readonly menuId = signal<string | null>(null);

  protected readonly confirmOpen = computed(() => this.toDelete() !== null);
  protected readonly confirmTitle = computed(() => `Delete ${this.toDelete()?.name ?? ''}?`);
  protected readonly confirmText = computed(
    () =>
      `${this.toDelete()?.name ?? 'This pilot'} will be removed from your pilot list and from the flight-preparation picker. This can't be undone.`,
  );

  /** First letters of the first two words of the name, e.g. "Lotte Peeters" gives "LP". */
  protected initials(name: string): string {
    return name
      .split(/\s+/)
      .filter(Boolean)
      .map((w) => Array.from(w)[0].toUpperCase())
      .join('')
      .slice(0, 2);
  }

  protected toggleMenu(id: string): void {
    this.menuId.update((cur) => (cur === id ? null : id));
  }

  protected onDocumentClick(event: Event): void {
    if (this.menuId() !== null && !(event.target as HTMLElement).closest('.menu-wrap')) this.menuId.set(null);
  }

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
    this.menuId.set(null);
    this.mode.set(mode);
  }

  protected cancel(): void {
    if (this.saving()) return;
    this.mode.set(null);
  }

  protected invalid(name: keyof typeof this.form.controls): boolean {
    const c = this.form.controls[name];
    return c.invalid && (c.touched || this.submitted());
  }

  protected onEnter(event: Event): void {
    event.preventDefault();
    void this.save();
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
    this.menuId.set(null);
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
