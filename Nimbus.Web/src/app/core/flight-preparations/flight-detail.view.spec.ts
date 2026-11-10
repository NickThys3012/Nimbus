import type { FlightDetailDto } from '../api-client';
import { buildDetailSections, NONE } from './flight-detail.view';
import { sampleDetail } from './testing/flight-detail.fixture';
import { FLIGHT_PREPARATION_SECTIONS } from './flight-preparation.model';

const value = (sections: ReturnType<typeof buildDetailSections>, id: string, testId: string) =>
  sections.flatMap((s) => s.rows).find((r) => r.testId === testId && sections.find((s) => s.id === id)?.rows.includes(r))?.value;

describe('buildDetailSections', () => {
  it('has every section of the form, in the same order', () => {
    const sections = buildDetailSections(sampleDetail());
    expect(sections.map((s) => s.id)).toEqual(FLIGHT_PREPARATION_SECTIONS.map((s) => s.id));
    expect(sections.map((s) => s.order)).toEqual([1, 2, 3, 4, 5, 6, 7, 8, 9]);
    expect(sections.map((s) => s.title)).toEqual(FLIGHT_PREPARATION_SECTIONS.map((s) => s.title));
  });

  it('shows names, times and sunrise/sunset readably', () => {
    const s = buildDetailSections(sampleDetail());
    expect(value(s, 'General', 'flight-time')).toBe('18:45');
    expect(value(s, 'General', 'sunrise')).toBe('06:12');
    expect(value(s, 'General', 'sunset')).toBe('21:03');
    expect(value(s, 'General', 'balloon')).toBe('OO-ABC');
    expect(value(s, 'General', 'pilot')).toBe('Alice');
    expect(value(s, 'General', 'launch-location')).toBe('Weelde (EBWE)');
  });

  it('formats the weather with units and keeps METAR/TAF and the wind levels', () => {
    const meteo = buildDetailSections(sampleDetail()).find((x) => x.id === 'Meteo')!;
    expect(meteo.rows.find((r) => r.testId === 'wind')?.value).toBe('6.5 kt · 270°');
    expect(meteo.rows.find((r) => r.testId === 'qnh')?.value).toBe('1018 hPa');
    expect(meteo.blocks.map((b) => b.value)).toEqual(['EBBR 081650Z', 'TAF EBBR']);
    expect(meteo.tables[0].rows).toEqual([['100 m', '90°', '5 kt', '12 °C']]);
  });

  it('reads the computed weights and the lift verdict from the single assessment, set apart as computed', () => {
    const load = buildDetailSections(sampleDetail()).find((x) => x.id === 'LoadCalculation')!;
    const computed = Object.fromEntries(load.rows.filter((r) => r.computed).map((r) => [r.testId, r.value]));
    expect(computed).toEqual({ 'passengers-weight': '130 kg', 'total-weight': '350 kg', 'lift-margin': '+150 kg', 'lift-verdict': 'Sufficient' });
    expect(load.rows.find((r) => r.testId === 'basket-weight')?.computed).toBeUndefined();
    expect(load.tables[0].rows).toEqual([['Bob', '80 kg', 'needs assistance']]);
  });

  it('says so when lift is overloaded or was never entered', () => {
    const lift = (assessment: FlightDetailDto['assessment']) =>
      buildDetailSections(sampleDetail({ assessment })).find((x) => x.id === 'LoadCalculation')!.rows.find((r) => r.testId === 'lift-verdict')!.value;
    expect(lift({ totalWeightKg: 600, passengersWeightKg: 0, marginKg: -100, liftSufficient: false, goNoGoVerdict: 'red' })).toBe('Overloaded');
    expect(lift({ totalWeightKg: 0, passengersWeightKg: 0, marginKg: 0, liftSufficient: null, goNoGoVerdict: 'unknown' })).toBe('Total lift not entered');
  });

  it('turns the checklist answers into words and the cruise altitudes into feet', () => {
    const s = buildDetailSections(sampleDetail());
    expect(value(s, 'Checklists', 'first-aid')).toBe('Yes');
    expect(value(s, 'Checklists', 'passenger-list')).toBe('N/A');
    expect(value(s, 'Checklists', 'flight-plan')).toBe('No');
    expect(value(s, 'Route', 'cruise-altitudes')).toBe('3000 ft, 4000 ft');
  });

  it('shows a dash for anything empty, never "null" or "undefined"', () => {
    const s = buildDetailSections(sampleDetail({ balloonRegistration: null, pilotName: null, launchLocationName: null, launchLocationIcaoCode: null }, {
      sunriseTime: null, windSpeedKt: null, metar: '  ', routeDescription: null, cruiseAltitudesFtCsv: '', windLevels: [], passengers: [], paxBriefingText: '<p><br></p>'.replace(/./g, ''),
    }));
    const all = s.flatMap((x) => x.rows.map((r) => r.value));
    expect(all.join('|')).not.toMatch(/null|undefined|NaN/);
    expect(value(s, 'General', 'balloon')).toBe(NONE);
    expect(value(s, 'General', 'sunrise')).toBe(NONE);
    expect(value(s, 'Meteo', 'wind')).toBe(NONE);
    expect(value(s, 'Route', 'cruise-altitudes')).toBe(NONE);
    expect(s.find((x) => x.id === 'Meteo')!.blocks[0].value).toBe(NONE);
    expect(s.find((x) => x.id === 'Meteo')!.tables).toEqual([]);
    expect(s.find((x) => x.id === 'PaxBriefing')!.html).toBeNull();
  });

  it('keeps the briefing html for the rich-text view and groups images by section', () => {
    const s = buildDetailSections(sampleDetail({
      images: [
        { id: 'i1', section: 'Meteo', contentType: 'image/png', url: 'https://files.test/a.png' },
        { id: 'i2', section: 'Route', contentType: 'image/png', url: 'https://files.test/b.png' },
      ],
    }));
    expect(s.find((x) => x.id === 'PaxBriefing')!.html).toBe('<h3>Landing</h3>');
    expect(s.find((x) => x.id === 'Meteo')!.images.map((i) => i.id)).toEqual(['i1']);
    expect(s.find((x) => x.id === 'Route')!.images.map((i) => i.id)).toEqual(['i2']);
    expect(s.find((x) => x.id === 'General')!.images).toEqual([]);
  });
});
