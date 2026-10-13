import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { startWith } from 'rxjs';

import { Banner } from '../../../components/banner/banner';
import { Button } from '../../../components/button/button';
import { EmptyState } from '../../../components/empty-state/empty-state';
import { LocationMap } from '../../../components/location-map/location-map';
import { Modal } from '../../../components/modal/modal';
import { type LaunchLocationDto } from '../../../core/api-client';
import { LaunchLocationsStore } from '../../../core/launch-locations/launch-locations.store';

/** Same real ranges the API enforces. */
const MIN_ELEVATION = -500;
const MAX_ELEVATION = 9000;

type FormMode = null | 'new' | string; // null = list, 'new' = adding, otherwise the id being edited

/** A blank or non-numeric entry is "no value yet" for the map preview. */
function toNumber(v: unknown): number | null {
  if (v === null || v === undefined || v === '') return null;
  const n = Number(v);
  return Number.isFinite(n) ? n : null;
}

@Component({
  selector: 'Nimbus-launch-locations',
  imports: [ReactiveFormsModule, Banner, Button, EmptyState, LocationMap, Modal],
  templateUrl: './launch-locations.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './launch-locations.css',
  host: { '(document:click)': 'onDocumentClick($event)', '(document:keydown.escape)': 'menuId.set(null)' },
})
export default class LaunchLocations {
  private readonly store = inject(LaunchLocationsStore);
  private readonly fb = inject(NonNullableFormBuilder);

  protected readonly locations = this.store.locations;
  protected readonly list = this.store.list;
  protected readonly saving = this.store.saving;
  protected readonly formError = this.store.saveError;
  protected readonly fieldErrors = this.store.fieldErrors;
  protected readonly listError = this.store.listError;
  protected readonly deleting = this.store.deleting;

  protected readonly mode = signal<FormMode>(null);
  protected readonly submitted = signal(false);
  protected readonly toDelete = signal<LaunchLocationDto | null>(null);

  protected readonly form = this.fb.group({
    name: ['', [Validators.required, Validators.maxLength(100), Validators.pattern(/\S/)]],
    icaoCode: ['', [Validators.pattern(/^\s*[A-Za-z0-9]{4}\s*$|^\s*$/)]],
    latitude: ['', [Validators.required, Validators.min(-90), Validators.max(90)]],
    longitude: ['', [Validators.required, Validators.min(-180), Validators.max(180)]],
    elevationM: ['', [Validators.min(MIN_ELEVATION), Validators.max(MAX_ELEVATION)]],
    airspaceNotes: ['', [Validators.maxLength(2000)]],
  });

  private readonly formValue = toSignal(this.form.valueChanges.pipe(startWith(this.form.getRawValue())), {
    initialValue: this.form.getRawValue(),
  });
  /** Live coordinates for the preview map; the map itself decides whether they are a valid pin. */
  protected readonly previewLat = computed(() => toNumber(this.formValue().latitude));
  protected readonly previewLon = computed(() => toNumber(this.formValue().longitude));

  private readonly formStatus = toSignal(this.form.statusChanges.pipe(startWith(this.form.status)), { initialValue: this.form.status });
  /** Shown under the preview once a save was attempted with errors. */
  protected readonly showWarning = computed(() => this.submitted() && this.formStatus() !== 'VALID');

  protected readonly formOpen = computed(() => this.mode() !== null);
  protected readonly isNew = computed(() => this.mode() === 'new');
  protected readonly title = computed(() => (this.isNew() ? 'Add launch location' : 'Edit launch location'));
  protected readonly subtitle = computed(() => {
    const n = this.list().length;
    return `${n} ${n === 1 ? 'LOCATION' : 'LOCATIONS'} · DRIVE METAR/TAF, FORECAST & SUNRISE`;
  });

  /** Id of the location whose three-dots menu (mobile) is open. */
  protected readonly menuId = signal<string | null>(null);

  protected readonly confirmOpen = computed(() => this.toDelete() !== null);
  protected readonly confirmTitle = computed(() => `Delete ${this.toDelete()?.name ?? ''}?`);
  protected readonly confirmText = computed(
    () => `${this.toDelete()?.name ?? 'This location'} will be removed from your launch locations. This can't be undone.`,
  );

  protected num(value: number | string): number {
    return Number(value);
  }

  /** Elevation is optional: null shows as an em dash. */
  protected elevation(l: LaunchLocationDto): string {
    return l.elevationM === null ? '—' : `${Number(l.elevationM)} m`;
  }

  protected toggleMenu(id: string): void {
    this.menuId.update((cur) => (cur === id ? null : id));
  }

  protected onDocumentClick(event: Event): void {
    if (this.menuId() !== null && !(event.target as HTMLElement).closest('.menu-wrap')) this.menuId.set(null);
  }

  protected add(): void {
    this.open('new', { name: '', icaoCode: '', latitude: '', longitude: '', elevationM: '', airspaceNotes: '' });
  }

  protected edit(l: LaunchLocationDto): void {
    this.open(l.id, {
      name: l.name,
      icaoCode: l.icaoCode ?? '',
      latitude: String(Number(l.latitude)),
      longitude: String(Number(l.longitude)),
      elevationM: l.elevationM === null ? '' : String(Number(l.elevationM)),
      airspaceNotes: l.airspaceNotes ?? '',
    });
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
    // Enter in the notes textarea is a new line, not a submit.
    if ((event.target as HTMLElement).tagName === 'TEXTAREA') return;
    event.preventDefault();
    void this.save();
  }

  protected async save(): Promise<void> {
    this.submitted.set(true);
    const mode = this.mode();
    if (this.form.invalid || mode === null) return;
    const v = this.form.getRawValue();
    const icao = v.icaoCode.trim().toUpperCase();
    const notes = v.airspaceNotes.trim();
    const elevation = v.elevationM === null || String(v.elevationM).trim() === '' ? null : Number(v.elevationM);
    const body = {
      name: v.name.trim(),
      icaoCode: icao === '' ? null : icao,
      latitude: Number(v.latitude),
      longitude: Number(v.longitude),
      elevationM: elevation,
      airspaceNotes: notes === '' ? null : notes,
    };
    if (await this.store.save(mode === 'new' ? null : mode, body)) {
      this.mode.set(null);
    }
  }

  protected askDelete(l: LaunchLocationDto): void {
    this.menuId.set(null);
    this.toDelete.set(l);
  }

  protected closeDialog(): void {
    if (this.deleting()) return;
    this.toDelete.set(null);
  }

  protected async confirmDelete(): Promise<void> {
    const l = this.toDelete();
    if (!l) return;
    await this.store.remove(l.id); // a failure shows up as the list error
    this.toDelete.set(null);
  }
}
