# Weather (epic EP-WEATHER)

## METAR and TAF (issue #44)

`GET /api/weather/metar-taf?icao=EBAW` returns `MetarTafDto { station, metar, taf }`: the current METAR and TAF
of an ICAO station as raw text, from [aviationweather.gov](https://aviationweather.gov/data/api/). Nothing is
stored: the form fills its METAR and TAF fields from the answer, so a failed fetch cannot change anything.

| Outcome | Answer |
|---|---|
| Station publishes a METAR and/or a TAF | `200`, each part as text, a part the station does not publish is `null` (many small fields have no TAF) |
| Station publishes nothing | `200`, both parts `null` |
| Malformed code (not four letters or digits) | `422` |
| Provider unreachable, too slow, an error status, an oversized reply | `502`, and a warning is logged |

### How a report is chosen (`MetarTafParser`)
The provider can return several stations in one response and several observations of one station, so the
station is picked by matching its ICAO code at the start of the report, case-insensitively and as a whole word
(`EBAW` does not match `EBAWX`). Behaviour that was checked against the live API:

- A METAR line is `METAR <ICAO> …` (or `SPECI`); the leading word is dropped. Observations of a station are
  listed newest first, so the first match is the current one.
- A TAF starts at a line beginning `TAF` (also `TAF AMD` and `TAF COR`) and continues over the indented lines
  that follow. It is kept as its full multi-line text: trailing blanks and empty lines are dropped, the
  continuation indent is kept. Only the requested station's forecast is taken, never its neighbours'.
- A station with no METAR is a `204`; a station with no TAF is a `200` with an empty body. Both mean "nothing".

### The client (`AviationWeatherClient`)
Its own typed `HttpClient` with its own base address, timeout and user agent, all configuration
(`AviationWeather` section, e.g. `AviationWeather__TimeoutSeconds` as an environment variable):

| Key | Default |
|---|---|
| `AviationWeather:BaseUrl` | `https://aviationweather.gov/api/data/` (must be https) |
| `AviationWeather:TimeoutSeconds` | `8` (1 to 60) |
| `AviationWeather:UserAgent` | `Nimbus/1.0 (flight preparation)` |

It returns a `ProviderResult<T>` and never throws for a provider problem: a failure is a result plus a logged
warning (`Aviation weather {Kind} fetch failed for {Icao}: {Reason}`); only the caller's own cancellation
propagates. The METAR and TAF requests run side by side, and if either fails the whole fetch fails, so the
pilot is told instead of getting half an answer. A reply over 256 K characters is refused.

Not here yet, and tracked by #48 (resilient clients for every provider): retries with backoff and jitter and
a circuit breaker. Today a failure is reported at once and the pilot can simply press the button again.

### In the form (section 02 · Meteo)
One **Fetch METAR & TAF** button above the METAR field, using the ICAO code of the launch location picked in
section 01.

- No ICAO code: the button is disabled and the reason is shown next to it (a different one when no location
  is picked yet).
- Success fills the METAR and TAF as if typed (the section counts as edited and the draft is kept), replacing
  what was there. A part the station does not publish leaves that field alone.
- Failure, or nothing published: a message, and **no field changes or clears**.
- A repeated click while waiting sends one request. A reply that arrives after the pilot picked another
  location is dropped, so a report never lands in the wrong flight.

### Storage
Fetched reports are stored whole, so the columns were widened: `Metar` 500 → 1000 and `Taf` 1000 → 2000
characters (a TAF with many change groups, or a METAR with long remarks, must fit).
