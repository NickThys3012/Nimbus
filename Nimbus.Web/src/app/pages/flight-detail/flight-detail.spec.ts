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

  it('shows all nine sections, in the form order, with no editable control anywhere', async () => {
    const { el } = await setup(sampleDetail());

    const ids = [...el.querySelectorAll('[data-section]')].map((s) => s.getAttribute('data-section'));
    expect(ids).toEqual(['General', 'Meteo', 'Airspace', 'Checklists', 'LoadCalculation', 'Route', 'Bulletin', 'Ofp', 'PaxBriefing']);
    expect(el.querySelectorAll('input, select, textarea, [contenteditable="true"]').length).toBe(0);
  });

  it('shows the title, the Go/No-Go badge, the computed weights and sunrise/sunset', async () => {
    const { q } = await setup(sampleDetail());

    expect(q('title')?.textContent).toContain('Weelde · 2026-08-08');
    expect(q('verdict-badge')?.textContent).toContain('GO');
    expect(q('verdict-badge')?.className).toContain('go');
    expect(q('total-weight')?.textContent).toContain('350 kg');
    expect(q('passengers-weight')?.textContent).toContain('130 kg');
    expect(q('lift-margin')?.textContent).toContain('+150 kg');
    expect(q('sun')?.textContent).toContain('06:12');
    expect(q('sun')?.textContent).toContain('21:03');
    expect(q('flown-badge')).toBeNull();
  });

  it('marks a flown flight and a no-go verdict', async () => {
    const { q } = await setup(sampleDetail({ status: 'Flown', assessment: { totalWeightKg: 1, passengersWeightKg: 0, marginKg: -5, liftSufficient: false, goNoGoVerdict: 'red' } }));

    expect(q('flown-badge')).not.toBeNull();
    expect(q('verdict-badge')?.textContent).toContain('NO-GO');
  });

  it('offers Edit to the owner, pointing at the form', async () => {
    const { q } = await setup(sampleDetail());

    const edit = q('action-edit')?.querySelector('a');
    expect(edit?.getAttribute('href')).toBe('/flight-preparations/f1');
    expect(q('shared-by')).toBeNull();
  });

  it('shows a shared viewer no Edit, and says whose flight it is and that it is read only', async () => {
    const { q } = await setup(sampleDetail({
      viewerRole: 'Shared', ownerName: 'Bob Builder',
      permissions: { canEdit: false, canDelete: false, canShare: false, canMarkFlown: false, canExport: true },
    }));

    expect(q('action-edit')).toBeNull();
    expect(q('shared-by')?.textContent).toContain('Shared with you by Bob Builder');
    expect(q('shared-by')?.textContent).toContain('read only');
  });

  it('shows an admin no Edit either, and does not call the flight shared', async () => {
    const { q } = await setup(sampleDetail({
      viewerRole: 'Admin', ownerName: 'Bob Builder',
      permissions: { canEdit: false, canDelete: false, canShare: false, canMarkFlown: false, canExport: true },
    }));

    expect(q('action-edit')).toBeNull();
    expect(q('shared-by')?.textContent).toContain('Owned by Bob Builder');
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
