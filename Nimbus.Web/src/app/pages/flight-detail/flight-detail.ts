import { ChangeDetectionStrategy, Component, computed, effect, inject, signal } from '@angular/core';
import { rxResource, toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { firstValueFrom, map } from 'rxjs';

import { Banner } from '../../components/banner/banner';
import { Button } from '../../components/button/button';
import { Modal } from '../../components/modal/modal';
import { RichTextView } from '../../components/rich-text-view/rich-text-view';
import { ApiError, FlightPreparationsService } from '../../core/api-client';
import { FlightPreparationDraftStore } from '../../core/flight-preparations/flight-preparation-draft.store';
import { buildDetailGroups, buildDetailHeader } from '../../core/flight-preparations/flight-detail.view';
import { verdictClass, verdictLabel } from '../../core/flight-preparations/verdict';

/**
 * The read-only dossier (issue #41): every section of the form, in the same order, plus the
 * Go/No-Go badge, the computed weights, sunrise and sunset, and the uploaded images inline. Nothing
 * here is editable. Who may open a flight, and which actions they get, is decided by the API; this
 * page only shows what it is told. A flight the viewer may not open (403) or that is gone (404)
 * sends them back to the flight list.
 *
 * Delete (issue #43) is offered only when the API says `canDelete`, behind a confirmation that
 * names the flight and says plainly that the delete is permanent.
 */
@Component({
  selector: 'Nimbus-flight-detail',
  imports: [Banner, Button, Modal, RichTextView, RouterLink],
  templateUrl: './flight-detail.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './flight-detail.css',
})
export default class FlightDetail {
  private readonly api = inject(FlightPreparationsService);
  private readonly router = inject(Router);
  private readonly drafts = inject(FlightPreparationDraftStore);

  private readonly id = toSignal(inject(ActivatedRoute).paramMap.pipe(map((p) => p.get('id') ?? '')), { initialValue: '' });

  protected readonly flight = rxResource({
    params: () => this.id() || undefined,
    stream: ({ params }) => this.api.getApiFlightPreparationsDetail({ id: params }),
  });

  protected readonly data = computed(() => (this.flight.hasValue() ? this.flight.value() : null));
  protected readonly header = computed(() => {
    const d = this.data();
    return d ? buildDetailHeader(d) : null;
  });
  protected readonly groups = computed(() => {
    const d = this.data();
    return d ? buildDetailGroups(d) : [];
  });
  protected readonly isFlown = computed(() => this.data()?.status === 'Flown');
  protected readonly verdict = computed(() => this.data()?.assessment.goNoGoVerdict ?? 'unknown');
  /** A real failure (not "not yours" and not "gone"), worth a retry. */
  protected readonly failed = computed(() => this.flight.error() !== undefined && !this.leavesForTheList(this.flight.error()));

  protected readonly confirmingDelete = signal(false);
  protected readonly deleting = signal(false);
  protected readonly deleteError = signal<string | null>(null);
  protected readonly deleteTitle = computed(() => `Delete ${this.data()?.details.title?.trim() || 'this flight'}?`);
  protected readonly deleteText = computed(() => {
    const d = this.data();
    const whose = d?.viewerRole === 'Admin' && d.ownerName ? `It belongs to ${d.ownerName}. ` : '';
    return `${whose}This permanently deletes the flight with its passengers, wind levels, images and shares. This can't be undone.`;
  });

  protected readonly verdictLabel = verdictLabel;
  protected readonly verdictClass = verdictClass;

  constructor() {
    effect(() => {
      if (this.leavesForTheList(this.flight.error())) void this.router.navigate(['/flights'], { replaceUrl: true });
    });
  }

  protected askDelete(): void {
    this.deleteError.set(null);
    this.confirmingDelete.set(true);
  }

  protected closeDelete(): void {
    if (this.deleting()) return;
    this.confirmingDelete.set(false);
  }

  protected async confirmDelete(): Promise<void> {
    const id = this.data()?.id;
    if (!id || this.deleting()) return;
    this.deleting.set(true);
    this.deleteError.set(null);
    let gone = false;
    try {
      await firstValueFrom(this.api.deleteApiFlightPreparations({ id }));
      gone = true;
    } catch (error) {
      // 404: someone else deleted it first, so the result is the one the pilot wanted.
      if (error instanceof ApiError && error.status === 404) gone = true;
      else if (error instanceof ApiError && error.status === 403) this.deleteError.set('You are not allowed to delete this flight.');
      else this.deleteError.set('The flight could not be deleted. Nothing was removed; try again in a moment.');
    } finally {
      this.deleting.set(false);
    }
    if (gone) {
      // A local draft of a flight that no longer exists would only come back to haunt the form.
      this.drafts.removeAutosave(id);
      this.confirmingDelete.set(false);
      void this.router.navigate(['/flights'], { replaceUrl: true });
    }
  }

  protected retry(): void {
    this.flight.reload();
  }

  private leavesForTheList(error: unknown): boolean {
    const cause = (error as { cause?: unknown } | undefined)?.cause ?? error;
    return cause instanceof ApiError && (cause.status === 403 || cause.status === 404);
  }
}
