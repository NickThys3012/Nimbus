import {
  ChangeDetectionStrategy,
  Component,
  computed,
  ElementRef,
  inject,
  OnDestroy,
  signal,
} from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router } from '@angular/router';
import { debounceTime, map, Subscription } from 'rxjs';

import { Button } from '../../components/button/button';
import { SectionCard } from '../../components/section-card/section-card';
import { type CanComponentDeactivate } from '../../guards/unsaved-changes-guard';
import { BalloonsStore } from '../../core/balloons/balloons.store';
import { LaunchLocationsStore } from '../../core/launch-locations/launch-locations.store';
import {
  createFlightPreparationForm,
  type FlightPreparationForm,
  hydrateFlightPreparationForm,
  sectionCompletionStatus,
} from '../../core/flight-preparations/flight-preparation-form';
import { FlightPreparationDraftStore } from '../../core/flight-preparations/flight-preparation-draft.store';
import {
  FLIGHT_PREPARATION_SECTIONS,
  type FlightPreparationSectionId,
  NEW_FLIGHT_PREPARATION_ID,
  type SectionCompletionStatus,
  sectionFormKey,
} from '../../core/flight-preparations/flight-preparation.model';
import { AirspaceSection } from './sections/airspace-section/airspace-section';
import { BulletinSection } from './sections/bulletin-section/bulletin-section';
import { ChecklistsSection } from './sections/checklists-section/checklists-section';
import { GeneralSection } from './sections/general-section/general-section';
import { LoadCalculationSection } from './sections/load-calculation-section/load-calculation-section';
import { MeteoSection } from './sections/meteo-section/meteo-section';
import { OfpSection } from './sections/ofp-section/ofp-section';
import { PaxBriefingSection } from './sections/pax-briefing-section/pax-briefing-section';
import { RouteSection } from './sections/route-section/route-section';

/**
 * The nine-section flight-preparation dossier (issue #32): one route, one reactive form, nine
 * independently-expandable accordion sections. There is no save/load API yet (#31 was schema
 * only), so this screen is backed entirely by `FlightPreparationDraftStore` — a client-side,
 * per-user draft that survives a refresh. Swapping that store's `save`/`load` for real HTTP calls
 * once the backend endpoint exists (#40/#51-style) is the one seam this component depends on.
 */
@Component({
  selector: 'Nimbus-flight-preparation-form',
  imports: [
    ReactiveFormsModule,
    Button,
    SectionCard,
    GeneralSection,
    MeteoSection,
    AirspaceSection,
    ChecklistsSection,
    LoadCalculationSection,
    RouteSection,
    BulletinSection,
    OfpSection,
    PaxBriefingSection,
  ],
  templateUrl: './flight-preparation-form.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './flight-preparation-form.css',
})
export default class FlightPreparationFormPage implements CanComponentDeactivate, OnDestroy {
  private readonly fb = inject(NonNullableFormBuilder);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly draftStore = inject(FlightPreparationDraftStore);
  private readonly balloonsStore = inject(BalloonsStore);
  private readonly locationsStore = inject(LaunchLocationsStore);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);

  protected readonly sections = FLIGHT_PREPARATION_SECTIONS;

  private readonly initialRouteId = this.route.snapshot.paramMap.get('id') ?? NEW_FLIGHT_PREPARATION_ID;
  protected readonly flightId = signal<string>(NEW_FLIGHT_PREPARATION_ID);
  protected readonly form = signal<FlightPreparationForm>(createFlightPreparationForm(this.fb));
  /** Bumped on every (non-debounced) form change so computed signals re-read `.invalid`/`.touched`
   *  — those aren't signals themselves, following the same pattern as `InputField.controlStateVersion`. */
  private readonly formVersion = signal(0);
  protected readonly expandedIds = signal<ReadonlySet<FlightPreparationSectionId>>(new Set(['General']));
  protected readonly savedAt = signal<string | null>(null);

  private lastKnownId: string | null = null;
  private valueSub: Subscription | null = null;
  private autosaveSub: Subscription | null = null;

  protected readonly title = computed(() => {
    this.formVersion();
    const general = this.form().controls.general.getRawValue();
    const location = this.locationsStore.list().find((l) => l.id === general.launchLocationId);
    const balloon = this.balloonsStore.list().find((b) => b.id === general.balloonId);
    if (!location) return this.flightId() === NEW_FLIGHT_PREPARATION_ID ? 'New flight preparation' : 'Flight preparation';
    return balloon ? `${location.name} · ${balloon.registration}` : location.name;
  });

  constructor() {
    this.hydrate(this.initialRouteId);
    // Route param changes (including the id this component assigns to itself on first autosave,
    // guarded against by `lastKnownId`) rebuild the form for that flight's draft.
    this.route.paramMap
      .pipe(
        map((p) => p.get('id') ?? NEW_FLIGHT_PREPARATION_ID),
        takeUntilDestroyed(),
      )
      .subscribe((id) => {
        if (id === this.lastKnownId) return;
        this.hydrate(id);
      });
  }

  ngOnDestroy(): void {
    this.valueSub?.unsubscribe();
    this.autosaveSub?.unsubscribe();
  }

  /** `CanComponentDeactivate` — the route guard prompts when this is true (issue #32 AC). */
  hasUnsavedChanges(): boolean {
    return this.form().dirty;
  }

  protected isExpanded(id: FlightPreparationSectionId): boolean {
    return this.expandedIds().has(id);
  }

  protected setExpanded(id: FlightPreparationSectionId, value: boolean): void {
    this.expandedIds.update((current) => {
      const next = new Set(current);
      if (value) next.add(id);
      else next.delete(id);
      return next;
    });
  }

  protected sectionStatus(id: FlightPreparationSectionId): SectionCompletionStatus {
    this.formVersion();
    return sectionCompletionStatus(this.form().controls[sectionFormKey(id) as keyof FlightPreparationForm['controls']]);
  }

  protected sectionSummary(id: FlightPreparationSectionId): string | null {
    this.formVersion();
    const v = this.form().getRawValue();
    switch (id) {
      case 'General':
        return v.general.flightDate && v.general.flightTime ? `${v.general.flightDate} · ${v.general.flightTime}` : null;
      case 'Meteo':
        return v.meteo.windSpeedKt ? `${v.meteo.windSpeedKt} kt · ${v.meteo.windDirectionDeg}°` : null;
      case 'Checklists': {
        const total = v.checklists.items.length;
        const done = v.checklists.items.filter((i) => i.checked).length;
        return total > 0 ? `${done}/${total}` : null;
      }
      case 'LoadCalculation':
        return v.loadCalculation.envelopeWeightKg ? `${v.loadCalculation.envelopeWeightKg} kg` : null;
      default:
        return null;
    }
  }

  protected goBack(): void {
    this.router.navigate(['/flights']);
  }

  /** Validates the whole dossier; on failure, expands and focuses the first invalid section
   *  (issue #32 AC). On success, commits the draft and clears the dirty flag the deactivate guard
   *  watches — this is the seam a future POST/PUT `FlightPreparations` call replaces. */
  protected save(): void {
    const form = this.form();
    form.markAllAsTouched();
    this.formVersion.update((v) => v + 1);

    if (form.invalid) {
      const invalidSection = this.sections.find(
        (s) => form.controls[sectionFormKey(s.id) as keyof FlightPreparationForm['controls']].invalid,
      );
      if (invalidSection) {
        this.setExpanded(invalidSection.id, true);
        setTimeout(() => this.focusFirstInvalid(invalidSection.id));
      }
      return;
    }

    const id = this.ensureId();
    // TODO(#40/#51): replace this localStorage write with the real save endpoint once it exists.
    this.savedAt.set(this.draftStore.save(id, form.getRawValue()));
    form.markAsPristine();
  }

  private focusFirstInvalid(id: FlightPreparationSectionId): void {
    const el = this.host.nativeElement.querySelector<HTMLElement>(`[data-section="${id}"] .ng-invalid`);
    el?.focus();
  }

  private ensureId(): string {
    let id = this.flightId();
    if (id === NEW_FLIGHT_PREPARATION_ID) {
      id = this.draftStore.newId();
      this.flightId.set(id);
      this.lastKnownId = id;
      this.router.navigate(['/flight-preparations', id], { replaceUrl: true });
    }
    return id;
  }

  private hydrate(id: string): void {
    this.lastKnownId = id;
    this.valueSub?.unsubscribe();
    this.autosaveSub?.unsubscribe();

    const form = createFlightPreparationForm(this.fb);
    const draft = id === NEW_FLIGHT_PREPARATION_ID ? null : this.draftStore.load<ReturnType<FlightPreparationForm['getRawValue']>>(id);
    if (draft) hydrateFlightPreparationForm(form, this.fb, draft.value);
    form.markAsPristine();

    this.form.set(form);
    this.flightId.set(id);
    this.savedAt.set(draft?.updatedAt ?? null);
    this.expandedIds.set(new Set(['General']));

    this.valueSub = form.valueChanges.subscribe(() => this.formVersion.update((v) => v + 1));
    this.autosaveSub = form.valueChanges.pipe(debounceTime(400)).subscribe(() => {
      if (!form.dirty) return;
      const saveId = this.ensureId();
      this.savedAt.set(this.draftStore.save(saveId, form.getRawValue()));
    });
  }
}
