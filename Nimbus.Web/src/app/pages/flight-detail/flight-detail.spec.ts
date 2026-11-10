import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter, Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import { type Mock, vi } from 'vitest';

import FlightDetail from './flight-detail';
import { ApiError, type FlightDetailDto, FlightPreparationsService } from '../../core/api-client';
import { sampleDetail } from '../../core/flight-preparations/testing/flight-detail.fixture';

describe('FlightDetail', () => {
  let api: { getApiFlightPreparationsDetail: Mock };
  let navigate: Mock;

  const httpError = (status: number) =>
    new ApiError({ method: 'GET', url: '/x' } as never, { url: '', ok: false, status, statusText: '', body: null }, `HTTP ${status}`);

  async function setup(result: FlightDetailDto | Error) {
    api = { getApiFlightPreparationsDetail: vi.fn().mockReturnValue(result instanceof Error ? throwError(() => result) : of(result)) };
    navigate = vi.fn().mockResolvedValue(true);
    await TestBed.configureTestingModule({
      imports: [FlightDetail],
      providers: [
        provideRouter([]),
        { provide: FlightPreparationsService, useValue: api },
        { provide: ActivatedRoute, useValue: { paramMap: of(convertToParamMap({ id: 'f1' })) } },
      ],
    }).compileComponents();
    vi.spyOn(TestBed.inject(Router), 'navigate').mockImplementation(navigate as never);
    const fixture = TestBed.createComponent(FlightDetail);
    await fixture.whenStable();
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    const q = (id: string) => el.querySelector(`[data-testid="${id}"]`) as HTMLElement | null;
    return { fixture, el, q };
  }

  it('asks the detail endpoint for the flight in the route', async () => {
    await setup(sampleDetail());
    expect(api.getApiFlightPreparationsDetail).toHaveBeenCalledWith({ id: 'f1' });
  });

  it('shows every section in the form order, as one document, with no editable control anywhere', async () => {
    const { el, q } = await setup(sampleDetail());

    const ids = [...el.querySelectorAll('[data-section]')].map((s) => s.getAttribute('data-section'));
    expect(ids).toEqual(['General', 'Meteo', 'Airspace', 'Checklists', 'LoadCalculation', 'Route', 'Bulletin', 'Ofp', 'PaxBriefing']);
    expect(q('document')?.querySelectorAll('[data-section]').length).toBe(9);
    expect(el.querySelectorAll('input, select, textarea, [contenteditable="true"]').length).toBe(0);
  });

  it('shows the title and the one-line summary under it', async () => {
    const { q } = await setup(sampleDetail());

    expect(q('title')?.textContent).toContain('Weelde · 2026-08-08');
    expect(q('meta')?.textContent).toBe('Sat 8 Aug 2026 · 18:45 · OO-ABC Cameron Z-90 · Alice');
  });

  it('shows the status strip: total, lift, a coloured margin and the Go/No-Go cell', async () => {
    const { q } = await setup(sampleDetail());

    expect(q('strip-total')?.textContent).toContain('350');
    expect(q('strip-lift')?.textContent).toContain('500');
    expect(q('strip-margin')?.textContent).toContain('+150');
    expect(q('strip-margin')?.className).toContain('ok');
    expect(q('verdict-badge')?.textContent).toBe('GO');
    expect(q('status-gonogo')?.className).toContain('go');
    expect(q('flown-badge')).toBeNull();
  });

  it('shows sunrise and sunset, and the lift margin, in the document', async () => {
    const { q } = await setup(sampleDetail());

    expect(q('sun')?.textContent).toBe('06:12 / 21:03');
    expect(q('lift-margin')?.textContent).toBe('+150 kg — sufficient');
  });

  it('marks a flown flight and a no-go verdict', async () => {
    const { q } = await setup(sampleDetail({ status: 'Flown', assessment: { totalWeightKg: 1, passengersWeightKg: 0, marginKg: -5, liftSufficient: false, goNoGoVerdict: 'red' } }));

    expect(q('flown-badge')).not.toBeNull();
    expect(q('verdict-badge')?.textContent).toBe('NO-GO');
    expect(q('status-gonogo')?.className).toContain('nogo');
    expect(q('strip-margin')?.className).toContain('bad');
  });

  it('offers Edit to the owner, pointing at the form, with the "own flight" note and no role badge', async () => {
    const { q } = await setup(sampleDetail());

    expect(q('action-edit')?.querySelector('a')?.getAttribute('href')).toBe('/flight-preparations/f1');
    expect(q('readonly-note')?.textContent).toBe('own flight');
    expect(q('role-badge')).toBeNull();
  });

  it('shows a shared viewer no Edit, a "shared by" badge and the read-only note', async () => {
    const { q } = await setup(sampleDetail({
      viewerRole: 'Shared', ownerName: 'Bob Builder',
      permissions: { canEdit: false, canDelete: false, canShare: false, canMarkFlown: false, canExport: true },
    }));

    expect(q('action-edit')).toBeNull();
    expect(q('role-badge')?.textContent).toContain('shared by Bob Builder');
    expect(q('role-badge')?.className).toContain('shared');
    expect(q('readonly-note')?.textContent).toBe('read-only · no edit rights');
  });

  it('gives an admin the Edit action too, under an "admin view" badge', async () => {
    const { q } = await setup(sampleDetail({
      viewerRole: 'Admin', ownerName: 'Bob Builder',
      permissions: { canEdit: true, canDelete: true, canShare: true, canMarkFlown: true, canExport: true },
    }));

    expect(q('action-edit')).not.toBeNull();
    expect(q('role-badge')?.textContent).toContain('admin view');
    expect(q('role-badge')?.className).toContain('admin');
    expect(q('readonly-note')?.textContent).toBe('admin · all flights visible');
  });

  it('shows the checklists as tick lines and the passengers as a table', async () => {
    const { el, q } = await setup(sampleDetail());

    expect(q('check-safety')?.textContent).toContain('not done: flight plan filed');
    expect(q('check-safety')?.querySelector('.ti-alert-triangle')).not.toBeNull();
    expect(q('passengers')?.querySelectorAll('tbody tr').length).toBe(1);
    expect(el.querySelector('[data-testid="passengers"] .ti-first-aid-kit')).not.toBeNull();
  });

  it('renders uploaded images inline in their own section', async () => {
    const { el } = await setup(sampleDetail({
      images: [
        { id: 'i1', section: 'Meteo', contentType: 'image/png', url: 'https://files.test/stuve.png' },
        { id: 'i2', section: 'Route', contentType: 'image/png', url: 'https://files.test/track.png' },
      ],
    }));

    const meteo = el.querySelector('[data-section="Meteo"] [data-testid="inline-image"]') as HTMLImageElement;
    const route = el.querySelector('[data-section="Route"] [data-testid="inline-image"]') as HTMLImageElement;
    expect(meteo.getAttribute('src')).toBe('https://files.test/stuve.png');
    expect(route.getAttribute('src')).toBe('https://files.test/track.png');
    expect(el.querySelector('[data-section="General"] img')).toBeNull();
  });

  it('shows the passenger briefing as formatted text, and a script in it cannot execute', async () => {
    const g = globalThis as unknown as { __pwned?: boolean };
    g.__pwned = false;
    const { el, q } = await setup(sampleDetail({}, { paxBriefingText: '<h3>Landing</h3><script>globalThis.__pwned = true</script><img src="x" onerror="globalThis.__pwned = true">' }));

    expect(q('briefing')?.querySelector('h3')?.textContent).toBe('Landing');
    expect(el.querySelector('script')).toBeNull();
    expect(el.innerHTML).not.toContain('onerror');
    expect(g.__pwned).toBe(false);
  });

  it.each([403, 404])('sends the viewer back to the flight list on a %i', async (status) => {
    const { q } = await setup(httpError(status));

    expect(navigate).toHaveBeenCalledWith(['/flights'], { replaceUrl: true });
    expect(q('title')).toBeNull();
    expect(q('load-error')).toBeNull();
  });

  it('shows an error with a retry, and stays, when the server fails', async () => {
    const { fixture, q } = await setup(httpError(500));

    expect(navigate).not.toHaveBeenCalled();
    expect(q('load-error')?.textContent).toContain('Could not load this flight');

    api.getApiFlightPreparationsDetail.mockReturnValue(of(sampleDetail()));
    q('retry')!.click();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(q('title')).not.toBeNull();
    expect(q('load-error')).toBeNull();
  });
});
