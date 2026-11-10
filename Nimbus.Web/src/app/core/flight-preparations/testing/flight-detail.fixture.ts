import type { FlightDetailDto } from '../../api-client';

/** A complete, valid read-only flight for tests; override any part. */
export function sampleDetail(over: Partial<FlightDetailDto> = {}, details: Partial<FlightDetailDto['details']> = {}): FlightDetailDto {
  return {
    id: 'f1', status: 'Planned', flownAt: null, viewerRole: 'Owner',
    permissions: { canEdit: true, canDelete: true, canShare: true, canMarkFlown: true, canExport: true },
    ownerName: null, balloonRegistration: 'OO-ABC', balloonType: 'Cameron Z-90', pilotName: 'Alice', launchLocationName: 'Weelde', launchLocationIcaoCode: 'EBWE',
    assessment: { totalWeightKg: 350, passengersWeightKg: 130, marginKg: 150, liftSufficient: true, goNoGoVerdict: 'green' },
    images: [],
    details: {
      title: 'Weelde · 2026-08-08', balloonId: 'b', pilotId: 'p', launchLocationId: 'l', flightDate: '2026-08-08', flightTime: '18:45:00',
      sunriseTime: '06:12:00', sunsetTime: '21:03:00', fieldOwnerNotified: true,
      windSpeedKt: 6.5, windDirectionDeg: 270, visibilityKm: 10, capeJPerKg: 0, temperatureC: 18, dewPointC: 9, qnhHpa: 1018,
      metar: 'EBBR 081650Z', taf: 'TAF EBBR',
      windLevels: [{ altitudeM: 100, directionDeg: 90, speedKt: 5, temperatureC: 12 }],
      notamsChecked: true, firstAidAndExtinguisher: 1, passengerListFilled: 2, flightPlanFiled: 0, burnerTested: true,
      envelopeWeightKg: 118, basketWeightKg: 90, burnerWeightKg: 25, cylindersWeightKg: 60, maxAltitudeFt: 4500, liftUnits: 3, totalLiftKg: 500,
      passengers: [{ name: 'Bob', weightKg: 80, needsAssistance: true, isChild: false, needsTransport: false }],
      routeDescription: 'North', climbRateMetersPerSecond: 1.5, descentRateMetersPerSecond: 1, cruiseAltitudesFtCsv: '3000, 4000',
      bulletinText: 'Calm', ofpOperatorName: 'Nimbus Balloons', ofpPicWeightKg: 82, ofpFuelAvailableMinutes: 120, ofpFuelRequiredMinutes: 90, ofpLandingLocation: 'Field 2',
      paxBriefingText: '<h3>Landing</h3>', paxBriefingVersion: '2026-11', paxBriefingIsCustom: false,
      ...details,
    },
    ...over,
  };
}
