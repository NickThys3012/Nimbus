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
import { debounceTime, map, Observable, Subject, Subscription, take } from 'rxjs';

import { Banner } from '../../components/banner/banner';
import { Button } from '../../components/button/button';
import { Modal } from '../../components/modal/modal';
import { SectionCard } from '../../components/section-card/section-card';
import { formatDayMonthTime } from '../../core/format/dates';
import { type CanComponentDeactivate } from '../../guards/unsaved-changes-guard';
import { BalloonsStore } from '../../core/balloons/balloons.store';
import { LaunchLocationsStore } from '../../core/launch-locations/launch-locations.store';
import {
  countInvalidLeaves,
  createFlightPreparationForm,
  diffingSectionIds,
  type FlightPreparationForm,
  type FlightPreparationFormValue,
  hydrateFlightPreparationForm,
  nextFlightSlot,
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

/** One section with a validation error, as summarized in the save-error banner (mockup frame 2A). */
interface SectionErrorSummary {
  id: FlightPreparationSectionId;
  title: string;
  count: number;
}

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
    Banner,
    Button,
    Modal,
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

  /** One entry per section, in order, for the segmented progress bar (mockup frames 1A/1B/2A). */
  protected readonly progressSegments = computed(() => {
    this.formVersion();
    return this.sections.map((s) => ({ id: s.id, status: this.sectionStatus(s.id) }));
  });
  protected readonly completedCount = computed(
    () => this.progressSegments().filter((s) => s.status === 'complete').length,
  );

  /** The last explicitly-saved value, used as the baseline for dirty/draft diffing and as what
   *  "Discard" reverts to. `null` for a flight that's never been explicitly saved yet. */
  private readonly savedBaseline = signal<FlightPreparationFormValue | null>(null);
  protected readonly isExistingFlight = computed(() => this.savedBaseline() !== null);
  /** True right after hydrating from an autosave that diverges from the last saved version
   *  (issue #32 mockup frame 3A) — cleared once the pilot saves or discards the draft. */
  protected readonly restoredFromDraft = signal(false);
  protected readonly draftDiffSectionIds = signal<ReadonlySet<FlightPreparationSectionId>>(new Set());
  protected readonly draftRestoredAt = signal<string | null>(null);
  protected readonly draftRestoredMessage = computed(() => {
    const at = this.draftRestoredAt();
    const base =
      'We restored the changes you made before the page reloaded (draft kept on this device).';
    return at ? `${base} Saved at ${formatDayMonthTime(at)}.` : base;
  });

  /** Set after a failed save attempt; cleared on the next save attempt or on hydrate. Drives the
   *  save-error banner (mockup frame 2A). */
  protected readonly saveErrorSections = signal<SectionErrorSummary[] | null>(null);
  protected readonly saveErrorMessage = computed(() => {
    const sections = this.saveErrorSections();
    if (!sections || sections.length === 0) return null;
    const parts = sections.map((s) => `${s.title} (${s.count})`);
    const list =
      parts.length === 1
        ? parts[0]
        : `${parts.slice(0, -1).join(', ')} and ${parts[parts.length - 1]}`;
    const sectionWord = sections.length === 1 ? 'section has an error' : 'sections have errors';
    return `Not saved. ${sections.length} ${sectionWord}: ${list}. We opened the first one.`;
  });

  /** Sections edited (dirty) since the last hydrate/save — drives the sticky bar's dirty summary
   *  and the "Leave without saving?" dialog's body (mockup frames 1B/2B). */
  protected readonly dirtySections = computed(() => {
    this.formVersion();
    const form = this.form();
    return this.sections.filter((s) => form.controls[sectionFormKey(s.id) as keyof FlightPreparationForm['controls']].dirty);
  });
  protected readonly dirtySummary = computed(() => {
    const dirty = this.dirtySections();
    if (dirty.length === 0) return null;
    return `${dirty.length} section${dirty.length === 1 ? '' : 's'} edited · ${dirty.map((s) => s.title).join(', ')}`;
  });

  /** The "Leave without saving?" confirmation dialog (mockup frame 2B), opened by `confirmDiscard`
   *  when the route guard blocks navigation away from unsaved changes. */
  protected readonly deactivateDialogOpen = signal(false);
  protected readonly deactivateDestination = signal('');
  private deactivateDecision: Subject<boolean> | null = null;
  /** True while this component swaps `/new` for its own freshly-assigned id URL — that param
   *  change re-runs `canDeactivate`, but it isn't the pilot leaving, so the guard must not prompt. */
  private assigningId = false;

  private lastKnownId: string | null = null;
  private valueSub: Subscription | null = null;
  private autosaveSub: Subscription | null = null;
  private balloonSub: Subscription | null = null;

  protected readonly title = computed(() => {
    this.formVersion();
    const general = this.form().controls.general.getRawValue();
    const location = this.locationsStore.list().find((l) => l.id === general.launchLocationId);
    const balloon = this.balloonsStore.list().find((b) => b.id === general.balloonId);
    if (!location) return this.flightId() === NEW_FLIGHT_PREPARATION_ID ? 'New flight preparation' : 'Flight preparation';
    return balloon ? `${location.name} · ${balloon.registration}` : location.name;
  });

  /** The selected launch location's coordinates, for the weather and sunrise features. */
  readonly launchCoordinates = computed(() => {
    this.formVersion();
    const id = this.form().controls.general.controls.launchLocationId.value;
    const location = this.locationsStore.list().find((l) => l.id === id);
    return location ? { latitude: Number(location.latitude), longitude: Number(location.longitude) } : null;
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
    this.balloonSub?.unsubscribe();
  }

  /** `CanComponentDeactivate` — the route guard prompts when this is true (issue #32 AC). */
  hasUnsavedChanges(): boolean {
    return !this.assigningId && this.form().dirty;
  }

  /** `CanComponentDeactivate` — opens this component's own "Leave without saving?" dialog
   *  (mockup frame 2B) and resolves once the pilot picks save-and-leave, leave-without-saving, or
   *  stay. */
  confirmDiscard(destinationUrl: string): Observable<boolean> {
    this.deactivateDestination.set(destinationUrl);
    this.deactivateDialogOpen.set(true);
    this.deactivateDecision = new Subject<boolean>();
    return this.deactivateDecision.asObservable().pipe(take(1));
  }

  /** "Save and leave": saves first and only leaves if that succeeded (the dossier was valid). */
  protected saveAndLeave(): void {
    this.save();
    this.resolveDeactivate(this.form().valid);
  }

  protected leaveWithoutSaving(): void {
    this.resolveDeactivate(true);
  }

  protected stayOnPage(): void {
    this.resolveDeactivate(false);
  }

  private resolveDeactivate(leave: boolean): void {
    this.deactivateDialogOpen.set(false);
    this.deactivateDecision?.next(leave);
    this.deactivateDecision?.complete();
    this.deactivateDecision = null;
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

  /** `'edited'` once a section was changed since hydrate/save (edit case only — a brand new,
   *  never-saved flight has nothing to compare "edited" against); `'from-draft'` when the section
   *  still holds a restored-but-untouched autosave value that differs from the last saved one
   *  (mockup frames 1B/3A). */
  protected sectionBadge(id: FlightPreparationSectionId): 'edited' | 'from-draft' | null {
    this.formVersion();
    const group = this.form().controls[sectionFormKey(id) as keyof FlightPreparationForm['controls']];
    if (group.dirty) return this.isExistingFlight() ? 'edited' : null;
    if (this.draftDiffSectionIds().has(id)) return 'from-draft';
    return null;
  }

  protected goBack(): void {
    this.router.navigate(['/flights']);
  }

  /** Validates the whole dossier; on failure, shows the error banner and expands + focuses the
   *  first invalid section (issue #32 AC). On success, commits the draft and clears the dirty
   *  flag the deactivate guard watches — this is the seam a future POST/PUT `FlightPreparations`
   *  call replaces. */
  protected save(): void {
    const form = this.form();
    form.markAllAsTouched();
    this.formVersion.update((v) => v + 1);

    if (form.invalid) {
      const errorSections: SectionErrorSummary[] = this.sections
        .map((s) => ({
          id: s.id,
          title: s.title,
          count: countInvalidLeaves(form.controls[sectionFormKey(s.id) as keyof FlightPreparationForm['controls']]),
        }))
        .filter((s) => s.count > 0);
      this.saveErrorSections.set(errorSections);

      const invalidSection = errorSections[0];
      if (invalidSection) {
        this.setExpanded(invalidSection.id, true);
        setTimeout(() => this.focusFirstInvalid(invalidSection.id));
      }
      return;
    }

    this.saveErrorSections.set(null);
    const id = this.ensureId();
    const value = form.getRawValue();
    // TODO(#40/#51): replace this localStorage write with the real save endpoint once it exists.
    this.savedAt.set(this.draftStore.save(id, value));
    this.draftStore.removeAutosave(id);
    this.savedBaseline.set(value);
    this.restoredFromDraft.set(false);
    this.draftDiffSectionIds.set(new Set());
    form.markAsPristine();
  }

  /** Discards the restored autosave (mockup frame 3A "Discard draft"): reverts to the last saved
   *  version (or an empty form for a flight that was never saved). */
  protected discardDraft(): void {
    const id = this.flightId();
    this.draftStore.removeAutosave(id);

    const form = createFlightPreparationForm(this.fb);
    const baseline = this.savedBaseline();
    if (baseline) hydrateFlightPreparationForm(form, this.fb, baseline);
    form.markAsPristine();

    this.replaceForm(form);
    this.restoredFromDraft.set(false);
    this.draftDiffSectionIds.set(new Set());
    this.draftRestoredAt.set(null);
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
      this.assigningId = true;
      void this.router
        .navigate(['/flight-preparations', id], { replaceUrl: true })
        .finally(() => (this.assigningId = false));
    }
    return id;
  }

  private hydrate(id: string): void {
    this.lastKnownId = id;

    const saved =
      id === NEW_FLIGHT_PREPARATION_ID ? null : this.draftStore.load<FlightPreparationFormValue>(id);
    const autosave = id === NEW_FLIGHT_PREPARATION_ID ? null : this.draftStore.loadAutosave<FlightPreparationFormValue>(id);

    // An autosave restores over the saved baseline when it's newer and actually differs from it —
    // that's the "draft restored after a refresh" case (mockup frame 3A). A brand new flight that
    // was never explicitly saved (saved === null) but already has an autosave is the same idea:
    // "nothing saved yet, here's what you had".
    let diffSections: FlightPreparationSectionId[] = [];
    let valueToUse = saved?.value ?? null;
    let restored = false;
    if (autosave && (!saved || autosave.updatedAt > saved.updatedAt)) {
      diffSections = saved ? diffingSectionIds(autosave.value, saved.value) : [];
      if (!saved || diffSections.length > 0) {
        valueToUse = autosave.value;
        restored = true;
      }
    }

    const form = createFlightPreparationForm(this.fb);
    if (valueToUse) hydrateFlightPreparationForm(form, this.fb, valueToUse);
    else form.controls.general.patchValue(nextFlightSlot());
    form.markAsPristine();

    this.replaceForm(form);
    this.flightId.set(id);
    this.savedAt.set(saved?.updatedAt ?? null);
    this.savedBaseline.set(saved?.value ?? null);
    this.restoredFromDraft.set(restored);
    this.draftDiffSectionIds.set(new Set(diffSections));
    this.draftRestoredAt.set(restored ? autosave!.updatedAt : null);
    this.saveErrorSections.set(null);
    this.expandedIds.set(new Set(['General']));
  }

  /** Swaps in a freshly-built form instance and (re)wires its change subscriptions — shared by
   *  `hydrate()` and `discardDraft()`. */
  private replaceForm(form: FlightPreparationForm): void {
    this.valueSub?.unsubscribe();
    this.autosaveSub?.unsubscribe();
    this.balloonSub?.unsubscribe();

    this.form.set(form);
    this.formVersion.update((v) => v + 1);

    this.balloonSub = form.controls.general.controls.balloonId.valueChanges.subscribe((id) =>
      this.snapshotBalloonWeights(form, id),
    );
    this.valueSub = form.valueChanges.subscribe(() => this.formVersion.update((v) => v + 1));
    this.autosaveSub = form.valueChanges.pipe(debounceTime(400)).subscribe(() => {
      if (!form.dirty) return;
      const saveId = this.ensureId();
      this.draftStore.saveAutosave(saveId, form.getRawValue());
    });
  }

  /** Copies the picked balloon's reference weights onto a flight that has not been saved yet, so
   *  later edits to the balloon never change the weights this flight was prepared with. Saved
   *  flights keep their snapshot. */
  private snapshotBalloonWeights(form: FlightPreparationForm, balloonId: string): void {
    if (this.isExistingFlight()) return;
    const balloon = this.balloonsStore.list().find((b) => b.id === balloonId);
    if (!balloon) return;
    form.controls.loadCalculation.patchValue({
      envelopeWeightKg: Number(balloon.envelopeWeightKg),
      basketWeightKg: Number(balloon.basketWeightKg),
      burnerWeightKg: Number(balloon.burnerWeightKg),
      cylindersWeightKg: Number(balloon.cylindersWeightKg),
    });
  }
}
