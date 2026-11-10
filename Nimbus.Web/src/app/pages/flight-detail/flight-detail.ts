import { ChangeDetectionStrategy, Component, computed, effect, inject } from '@angular/core';
import { rxResource, toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { map } from 'rxjs';

import { Banner } from '../../components/banner/banner';
import { Button } from '../../components/button/button';
import { RichTextView } from '../../components/rich-text-view/rich-text-view';
import { ApiError, FlightPreparationsService } from '../../core/api-client';
import { buildDetailGroups, buildDetailHeader } from '../../core/flight-preparations/flight-detail.view';
import { verdictClass, verdictLabel } from '../../core/flight-preparations/verdict';

/**
 * The read-only dossier (issue #41): every section of the form, in the same order, plus the
 * Go/No-Go badge, the computed weights, sunrise and sunset, and the uploaded images inline. Nothing
 * here is editable. Who may open a flight, and which actions they get, is decided by the API; this
 * page only shows what it is told. A flight the viewer may not open (403) or that is gone (404)
 * sends them back to the flight list.
 */
@Component({
  selector: 'Nimbus-flight-detail',
  imports: [Banner, Button, RichTextView, RouterLink],
  templateUrl: './flight-detail.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './flight-detail.css',
})
export default class FlightDetail {
  private readonly api = inject(FlightPreparationsService);
  private readonly router = inject(Router);

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

  protected readonly verdictLabel = verdictLabel;
  protected readonly verdictClass = verdictClass;

  constructor() {
    effect(() => {
      if (this.leavesForTheList(this.flight.error())) void this.router.navigate(['/flights'], { replaceUrl: true });
    });
  }

  protected retry(): void {
    this.flight.reload();
  }

  private leavesForTheList(error: unknown): boolean {
    const cause = (error as { cause?: unknown } | undefined)?.cause ?? error;
    return cause instanceof ApiError && (cause.status === 403 || cause.status === 404);
  }
}
