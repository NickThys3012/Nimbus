import type { FlightDetailDto } from '../api-client';
import { buildDetailGroups, buildDetailHeader, NONE } from './flight-detail.view';
import { sampleDetail } from './testing/flight-detail.fixture';

const groupOf = (flight: FlightDetailDto, id: string) => buildDetailGroups(flight).find((g) => g.id === id)!;
const rowOf = (flight: FlightDetailDto, id: string, testId: string) => groupOf(flight, id).rows.find((r) => r.testId === testId);

describe('buildDetailHeader', () => {
  it('summarises date, time, balloon with its type and the pilot on one line', () => {
    const h = buildDetailHeader(sampleDetail());
    expect(h.title).toBe('Weelde · 2026-08-08');
    expect(h.meta).toBe('Sat 8 Aug 2026 · 18:45 · OO-ABC Cameron Z-90 · Alice');
  });

  it('leaves out what is not known instead of printing a dash or "null"', () => {
    const h = buildDetailHeader(sampleDetail({ balloonRegistration: null, balloonType: null, pilotName: null }));
    expect(h.meta).toBe('Sat 8 Aug 2026 · 18:45');
  });

  it('has no role badge for the owner, and says who shared it with a shared viewer', () => {
    expect(buildDetailHeader(sampleDetail()).roleBadge).toBeNull();
    expect(buildDetailHeader(sampleDetail()).readonlyNote).toBe('own flight');

    const shared = buildDetailHeader(sampleDetail({ viewerRole: 'Shared', ownerName: 'Bob Builder' }));
    expect(shared.roleBadge).toEqual({ kind: 'shared', icon: 'ti-users-group', text: 'shared by Bob Builder' });
    expect(shared.readonlyNote).toBe('read-only · no edit rights');
  });

  it('marks an admin view', () => {
    const admin = buildDetailHeader(sampleDetail({ viewerRole: 'Admin', ownerName: 'Bob Builder' }));
    expect(admin.roleBadge).toEqual({ kind: 'admin', icon: 'ti-shield-check', text: 'admin view' });
    expect(admin.readonlyNote).toBe('admin · all flights visible');
  });

  it('shows total weight, lift and a coloured margin in the status strip', () => {
    const strip = Object.fromEntries(buildDetailHeader(sampleDetail()).strip.map((c) => [c.testId, c]));
    expect([strip['strip-total'].value, strip['strip-total'].unit]).toEqual(['350', 'kg']);
    expect([strip['strip-lift'].value, strip['strip-lift'].unit]).toEqual(['500', 'kg']);
    expect([strip['strip-margin'].value, strip['strip-margin'].tone]).toEqual(['+150', 'ok']);
  });

  it('shows an overloaded margin as bad, and dashes when no lift was entered', () => {
    const over = buildDetailHeader(sampleDetail({ assessment: { totalWeightKg: 600, passengersWeightKg: 0, marginKg: -100, liftSufficient: false, goNoGoVerdict: 'red' } }));
    expect(over.strip.find((c) => c.testId === 'strip-margin')).toMatchObject({ value: '−100', tone: 'bad' });

    const none = buildDetailHeader(sampleDetail({ assessment: { totalWeightKg: 0, passengersWeightKg: 0, marginKg: 0, liftSufficient: null, goNoGoVerdict: 'unknown' } }, { totalLiftKg: 0 }));
    expect(none.strip.find((c) => c.testId === 'strip-margin')).toMatchObject({ value: NONE, tone: undefined });
    expect(none.strip.find((c) => c.testId === 'strip-lift')?.value).toBe(NONE);
  });
});

describe('buildDetailGroups', () => {
  it('has every section of the form in its order, with the mockup headings', () => {
    const groups = buildDetailGroups(sampleDetail());
    expect(groups.map((g) => g.id)).toEqual(['General', 'Meteo', 'Airspace', 'Checklists', 'LoadCalculation', 'Route', 'Bulletin', 'Ofp', 'PaxBriefing']);
    expect(groups.map((g) => g.heading)).toEqual([
      '01 · general', '02 · meteo', '03 · airspace & NOTAMs', '04 · checklists', '05 · load calculation',
      '06 · route & trajectory', '07 · balloon bulletin', '08 · OFP', 'pax briefing',
    ]);
  });

  it('shows location, sunrise/sunset together and whether the field owner was told', () => {
    const d = sampleDetail();
    expect(rowOf(d, 'General', 'launch-location')?.value).toBe('Weelde (EBWE)');
    expect(rowOf(d, 'General', 'sun')?.value).toBe('06:12 / 21:03');
    expect(rowOf(d, 'General', 'field-owner-notified')).toMatchObject({ value: 'yes', tone: 'ok' });
    expect(rowOf(sampleDetail({}, { fieldOwnerNotified: false }), 'General', 'field-owner-notified')).toMatchObject({ value: 'no', tone: 'warn' });
  });

  it('formats the weather on combined lines, with METAR and TAF as quiet text and right-aligned wind columns', () => {
    const d = sampleDetail();
    expect(rowOf(d, 'Meteo', 'wind')?.value).toBe('6.5 kt / 270°');
    expect(rowOf(d, 'Meteo', 'visibility-cape')?.value).toBe('10 km / 0 J/kg');
    expect(rowOf(d, 'Meteo', 'temp-dew-qnh')?.value).toBe('18°C / 9°C / 1018 hPa');
    expect(rowOf(d, 'Meteo', 'metar')).toMatchObject({ value: 'EBBR 081650Z', tone: 'dim' });
    const table = groupOf(d, 'Meteo').tables[0];
    expect(table.columns.map((c) => [c.label, !!c.right])).toEqual([['ALTITUDE', false], ['DIRECTION', true], ['KT', true], ['°C', true]]);
    expect(table.rows[0].map((c) => c.text)).toEqual(['100 m', '90°', '5', '12']);
  });

  it('flags NOTAMs not checked in amber and leaves out empty optional notes', () => {
    expect(rowOf(sampleDetail(), 'Airspace', 'notams-checked')).toMatchObject({ value: 'YES', tone: 'ok' });
    const unchecked = sampleDetail({}, { notamsChecked: false, obstacles: 'Power line 800 m north.' });
    expect(rowOf(unchecked, 'Airspace', 'notams-checked')).toMatchObject({ value: 'NO', tone: 'warn' });
    expect(groupOf(unchecked, 'Airspace').rows.map((r) => r.testId)).toEqual(['notams-checked', 'obstacles']);
  });

  it('turns the checklists into two tick lines, naming what is not done', () => {
    const [safety, technical] = groupOf(sampleDetail(), 'Checklists').checks;
    expect(safety.text).toBe('first aid kit & extinguisher, passenger list filled (N/A) — not done: flight plan filed');
    expect(safety.done).toBe(false);
    expect(technical.text).toBe('burner — 1/5 — not done: gas cylinders, balloon visual, moorings, instruments');

    const all = groupOf(sampleDetail({}, { firstAidAndExtinguisher: 1, passengerListFilled: 1, flightPlanFiled: 2, burnerTested: true, gasCylindersChecked: true, balloonVisualInspectionDone: true, mooringsChecked: true, instrumentsChecked: true }), 'Checklists').checks;
    expect(all.map((c) => c.done)).toEqual([true, true]);
    expect(all[1].text).toBe('burner, gas cylinders, balloon visual, moorings, instruments — 5/5');
  });

  it('lists passengers with their marks, then the margin from the single assessment', () => {
    const d = sampleDetail({}, { passengers: [
      { name: 'M. Verhoeven', weightKg: 64, needsAssistance: true, isChild: false, needsTransport: false },
      { name: 'L. Peeters', weightKg: 32, needsAssistance: false, isChild: true, needsTransport: true },
    ] });
    const load = groupOf(d, 'LoadCalculation');
    expect(load.tables[0].rows[0][0]).toMatchObject({ text: 'M. Verhoeven', marks: [{ icon: 'ti-first-aid-kit', title: 'Needs assistance' }] });
    expect(load.tables[0].rows[1][0].marks?.map((m) => m.icon)).toEqual(['ti-baby-carriage', 'ti-car']);
    expect(load.tables[0].rows[1][1]).toEqual({ text: '32', right: true });
    expect(load.afterRows[0]).toMatchObject({ label: 'Margin', value: '+150 kg — sufficient', tone: 'ok' });
  });

  it('says overloaded in amber, and that lift was not entered in quiet text', () => {
    const margin = (assessment: FlightDetailDto['assessment']) => groupOf(sampleDetail({ assessment }), 'LoadCalculation').afterRows[0];
    expect(margin({ totalWeightKg: 600, passengersWeightKg: 0, marginKg: -100, liftSufficient: false, goNoGoVerdict: 'red' })).toMatchObject({ value: '−100 kg — overloaded', tone: 'warn' });
    expect(margin({ totalWeightKg: 0, passengersWeightKg: 0, marginKg: 0, liftSufficient: null, goNoGoVerdict: 'unknown' })).toMatchObject({ value: 'total lift not entered', tone: 'dim' });
  });

  it('keeps the balloon weight snapshot visible', () => {
    expect(rowOf(sampleDetail(), 'LoadCalculation', 'balloon-weights')?.value).toBe('envelope 118 · basket 90 · burner 25 · cylinders 60 kg');
  });

  it('joins cruise altitudes with slashes and hides zero climb and descent rates', () => {
    const d = sampleDetail({}, { climbRateMetersPerSecond: 0, descentRateMetersPerSecond: 0 });
    expect(rowOf(d, 'Route', 'cruise-altitudes')?.value).toBe('3000 / 4000 ft');
    expect(groupOf(d, 'Route').rows.map((r) => r.testId)).toEqual(['route-description', 'cruise-altitudes']);
    expect(rowOf(sampleDetail(), 'Route', 'climb-rate')?.value).toBe('1.5 m/s');
  });

  it('shows what the OFP is missing as "not filled in" in amber', () => {
    const d = sampleDetail({}, { ofpFuelAvailableMinutes: 0, ofpFuelRequiredMinutes: 0, ofpLandingLocation: '' });
    expect(rowOf(d, 'Ofp', 'ofp-operator')?.value).toBe('Nimbus Balloons / 82 kg');
    expect(rowOf(d, 'Ofp', 'ofp-fuel')).toMatchObject({ value: 'not filled in', tone: 'warn' });
    expect(rowOf(d, 'Ofp', 'ofp-landing-location')).toMatchObject({ value: 'not filled in', tone: 'warn' });
    expect(rowOf(sampleDetail(), 'Ofp', 'ofp-fuel')).toMatchObject({ value: '120 / 90 min' });
  });

  it('keeps the briefing html and a note saying which version it started from', () => {
    const standard = groupOf(sampleDetail(), 'PaxBriefing');
    expect(standard.html).toBe('<h3>Landing</h3>');
    expect(standard.headingNote).toBe('· standard 2026-11');
    expect(groupOf(sampleDetail({}, { paxBriefingIsCustom: true }), 'PaxBriefing').headingNote).toBe('· customized 2026-11');
    expect(groupOf(sampleDetail({}, { paxBriefingText: '' }), 'PaxBriefing').html).toBeNull();
  });

  it('shows the bulletin as a note, quiet when empty', () => {
    expect(groupOf(sampleDetail(), 'Bulletin').note).toEqual({ text: 'Calm', dim: false, testId: 'bulletin' });
    expect(groupOf(sampleDetail({}, { bulletinText: null }), 'Bulletin').note).toEqual({ text: NONE, dim: true, testId: 'bulletin' });
  });

  it('groups images by their section', () => {
    const d = sampleDetail({ images: [
      { id: 'i1', section: 'Meteo', contentType: 'image/png', url: 'https://files.test/a.png' },
      { id: 'i2', section: 'Route', contentType: 'image/png', url: 'https://files.test/b.png' },
    ] });
    expect(groupOf(d, 'Meteo').images.map((i) => i.id)).toEqual(['i1']);
    expect(groupOf(d, 'Route').images.map((i) => i.id)).toEqual(['i2']);
    expect(groupOf(d, 'General').images).toEqual([]);
  });

  it('never prints null, undefined or NaN for an empty dossier', () => {
    const d = sampleDetail({ balloonRegistration: null, pilotName: null, launchLocationName: null, launchLocationIcaoCode: null }, {
      sunriseTime: null, sunsetTime: null, windSpeedKt: null, visibilityKm: null, capeJPerKg: null, temperatureC: null, dewPointC: null, qnhHpa: null,
      metar: null, taf: null, windLevels: [], passengers: [], routeDescription: null, cruiseAltitudesFtCsv: '', envelopeWeightKg: null, maxAltitudeFt: null, liftUnits: null,
    });
    const all = buildDetailGroups(d).flatMap((g) => [...g.rows, ...g.afterRows].map((r) => r.value)).join('|');
    expect(all).not.toMatch(/null|undefined|NaN/);
  });
});
