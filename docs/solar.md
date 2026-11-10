# Sunrise and sunset (issue #49, epic EP-CALC)

`GET /api/solar/sun-times?latitude=&longitude=&date=&timeZone=` returns `SunTimesDto { date, condition, sunriseUtc,
solarNoonUtc, sunsetUtc, timeZone, sunrise, solarNoon, sunset }`. It is a pure calculation (`SolarCalculator` in
`Nimbus.Domain/Services`): no provider, nothing stored, the same answer offline. `date` is the local calendar date
(`yyyy-MM-dd`). The UTC instants are always there; `sunrise`, `solarNoon` and `sunset` (`HH:mm`) are there when an IANA
`timeZone` (e.g. `Europe/Brussels`) was given, with that date's daylight saving.

| Outcome | Answer |
|---|---|
| Normal day | `200`, `condition: "normal"` |
| Sun never sets / never rises | `200`, `condition: "polarDay"` / `"polarNight"`, times are edge values |
| Coordinates out of range, year outside 1900-2200, unknown time zone | `422` |

## The algorithm
The NOAA Solar Calculator method, each step a public function of `SolarCalculator` so it can be tested on its own: Julian
date and century, geometric mean longitude and anomaly, orbit eccentricity, equation of centre, apparent longitude, mean and
corrected obliquity, declination, equation of time, and the sunrise hour angle. The horizon is the official zenith
**90.833 degrees** (the sun's radius plus atmospheric refraction). Like NOAA's calculator it computes twice: the first estimate is
recomputed at the time it found.

**Polar day and night.** The cosine of the hour angle is clamped to [-1, 1] instead of passing a value outside it to `acos`
(which gives NaN). Polar night gives a hour angle of 0 (sunrise and sunset both at solar noon), polar day 180 degrees (12 h either
side of solar noon). `condition` says which, so callers do not take those edge values for real events. At the poles themselves
the quotient is 0/0; the sign of the declination decides.

Times are rounded to the minute. Accuracy is about a minute between roughly 72 degrees north and south; near the polar circles the sun
meets the horizon at a shallow angle, so differences of a few minutes between methods are normal there.

## How it was checked
`SolarCalculatorTests`:
- **Reference values**: sunrise and sunset in UTC from Open-Meteo's archive API, an independent source, for Brussels on both equinoxes, both
  solstices and 8 August (the date in the form mockup), Weelde, New York, Sydney, Wellington (the date line) and Reykjavik. All agree
  within one minute, except Reykjavik at the June solstice (2 minutes, asserted with a 3 minute tolerance and a comment).
- **Intermediate quantities** against published values: Meeus's Julian dates, the J2000 constants, and his declination and
  equation-of-time example 25.a.
- **Polar day and night** (Tromso), the poles, the equator, and rejected coordinates.

**Not done: the published Belgian AIP tables.** The acceptance criteria ask for a cross-check against AIP sunrise/sunset values on at
least four dates, but the AIP tables are not reachable from the development environment and were not typed from memory. Brussels
values from an independent source for four dates spread over the year stand in for them. To close this, add the published
values as `[TestCase]`s to `SolarCalculatorTests.MatchesTheReference_WithinOneMinute` (AIP times are UTC).

## In the form
Section 01 fills **Sunrise** and **Sunset** for the launch location and the flight date as soon as both are known (`SunTimesLoader`,
provided by the form page), with a line under them ("Calculated for 2026-08-08 (Europe/Brussels) with the NOAA solar-position
algorithm", or the polar-day/night explanation, or that it could not be calculated).

- It never overwrites a time the pilot typed: a field is written only when it is empty or still holds what the loader wrote last, so
  changing the date or the place refreshes it and a hand-entered time stays.
- Polar day and night write nothing (the edge values are not times to plan by).
- The fields stay editable, and the time zone is the **browser's**: the date and time of a flight are read in the pilot's own zone, as the
  wind-profile fetch does. A flight planned for a site in another zone would show the times in the pilot's zone, so the form
  is right for the usual case (planning where you fly) and the zone is the first thing to revisit if a launch site ever
  carries its own.
- The detail view shows what was saved (it already did, as `Sunrise / sunset`), so the calculated times are there once saved.
- There is no PDF export in the application yet, so there is nothing to show them on; when one is added it should print the
  same stored times.
