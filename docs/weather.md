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

Retries and a circuit breaker come from the shared provider policy (issue #48, below). The form waits on this fetch, so
lower `AviationWeather:Resilience:MaxRetries` if a slow provider should be reported sooner.

### In the form (section 02 · Meteo)
Built to the form mockup (`nimbus-prep-form.html`, in `../Nimbus mocups/`): a **Fetch** button with a refresh icon
sits in the **Meteo section's header**, on the right after a meta text such as **"EBAW · 17:40Z"** (the METAR's
station and observation time). It uses the ICAO code of the launch location picked in section 01.

- **Header, not body.** The button is there whether the section is open or collapsed. It lives in the page,
  with its state in `MetarTafFetcher` (provided by the form page), because the section card's header is a
  separate element from its body: the card has a `[card-action]` slot beside the toggle button, never inside it.
  The section body only shows how the last fetch went, or why none is possible.
- **Header text.** `metarMeta()` reads the station and time from the METAR text itself, so it is right after a
  reload, after an edit, and for a METAR that was pasted in. Text without a valid time group (free text that
  merely starts with a four-letter word) shows nothing and the header falls back to the wind summary.
- No ICAO code: the button is dimmed and `aria-disabled` (not `disabled`), with the reason in its tooltip.
  Clicking it opens the section, where the reason is written out (a different one when no location is picked
  yet). Not a hard `disabled`, so the click is never silently swallowed while the section is collapsed.
- Success opens the section and fills the METAR and TAF as if typed (the section counts as edited and the draft
  is kept), replacing what was there. A part the station does not publish leaves that field alone.
- Failure, or nothing published: a message, and **no field changes or clears**.
- A repeated click while waiting sends one request. A reply that arrives after the pilot picked another
  location is dropped, so a report never lands in the wrong flight.

Not in this story, although the mockup shows them filled after a fetch: the numeric fields (wind, visibility,
CAPE, temperature, dew point, QNH) and precipitation. They are filled from the forecast and METAR by the stories
that follow (#46 and #50). The mockup's Dutch "Haal op" reads "Fetch" here, and the card keeps its chevron like
the other sections.

### Storage
Fetched reports are stored whole, so the columns were widened: `Metar` 500 → 1000 and `Taf` 1000 → 2000
characters (a TAF with many change groups, or a METAR with long remarks, must fit).

## Wind profile by altitude (issue #46)

`GET /api/weather/wind-profile?latitude=&longitude=&hourUtc=&model=` returns
`WindProfileDto { model, forecastHourUtc, levels[] }`, each level `{ altitudeM, directionDeg, speedKt, temperatureC,
altitudeSource }`, lowest altitude first. The data comes from [Open-Meteo](https://open-meteo.com/) (knots, GMT, one
hour). Nothing is stored by the endpoint: the form fills its wind-on-altitude table (`WindLevel` rows) from it.
`hourUtc` defaults to the current hour, `model` to `icon_seamless`.

| Outcome | Answer |
|---|---|
| All levels returned | `200` |
| Model not in the supported list, or coordinates out of range | `422` |
| Model returns no data for a required level at that place/hour | `422` naming the missing levels (never zero-filled) |
| Provider unreachable, slow, error status, bad JSON | `502`, warning logged |

### Levels
- Low levels: 10, 20, 50, 100, 150, 200 m above ground (`altitudeSource: agl`). Open-Meteo has no temperature at
  these heights, so °C is the 2 m temperature lowered at the standard lapse rate (6.5 K/km).
- Pressure levels 950 / 925 / 900 / 850 hPa. The altitude is the model's `geopotential_height_{N}hPa`
  (`geopotential`); if the model returns none, the standard-atmosphere fallback 500 / 800 / 1000 / 1500 m is used
  (`standard`). Wind and temperature come from the same pressure level.
- Direction is normalised to 0-359; speed and temperature are rounded to one decimal.

### Models
Only models verified to return every pressure level and the low-altitude winds are accepted (`icon_seamless` default,
`icon_d2`, `icon_eu`, `icon_global`, `meteofrance_seamless`, `meteofrance_arome_france`, `meteofrance_arpege_europe`,
`meteofrance_arpege_world`, `cma_grapes_global`, `ukmo_uk_deterministic_2km`, `best_match`). Models with gaps (no 950 hPa,
no 200 m, no pressure levels) are rejected. The list lives in `WindProfileMapper.SupportedModels` and is mirrored in
`wind-profile-fetcher.ts`. Client config: `OpenMeteo:BaseUrl` / `TimeoutSeconds` / `UserAgent`.

### In the form (section 02 · Meteo)
Above the wind-on-altitude table: a model select (supported models only), a **Fetch wind** button and a note with the
last result or why a fetch is not possible (no launch location). It uses the launch location's coordinates and the
flight's date and time (current hour when not filled in). `+ Level` keeps working on the fetched rows.

- Fetched levels fill the table and mark it edited. A failed fetch (422 missing levels with the server's message, 502
  provider down, anything else) changes nothing.
- **Never overwrites the pilot's rows silently.** When the table has rows that are not blank and not exactly what the
  last fetch wrote, a confirmation dialog ("Replace your wind levels?" / Keep mine / Replace) appears first; rows still
  equal to the last fetch are replaced without asking.

## Hourly forecast and sunrise/sunset (issue #45)

`GET /api/weather/hourly-forecast?latitude=&longitude=&model=` returns `HourlyForecastDto { model, timeZone,
utcOffsetSeconds, days[], hours[] }` from [Open-Meteo](https://open-meteo.com/) (knots, three days). Nothing is stored.

- **Hour** `{ time, model, temperatureC, windSpeedKt, windDirectionDeg, windGustKt, capeJPerKg, precipitationMm,
  precipitationProbabilityPct }`. `time` is the launch site's **local** wall-clock time (`yyyy-MM-ddTHH:mm`); the
  timezone comes from `timezone=auto` (derived from the coordinates, nothing hard-coded). A value the model does not
  provide is `null` and shown as "–", never zero.
- **Day** `{ date, sunrise, sunset }`, `HH:mm` local (`daily=sunrise,sunset`), null when the sun does not rise or set.
- **Per-hour model.** A seamless model is requested as its parts (`icon_seamless` = `icon_d2` → `icon_eu` →
  `icon_global`, likewise `meteofrance_seamless`, `ukmo_seamless`; any other model is requested as is). Each hour
  takes the first part that has data and records it in `model`, so the UI marks where the resolution drops. Hours no
  part covers are left out.
- **Model** is configuration (`OpenMeteo:ForecastModel`, default `icon_seamless`; `OpenMeteo:ForecastDays`, default 3)
  and optionally per request, from `HourlyForecastModels.Supported` (mirrored in `hourly-forecast-fetcher.ts`).

| Outcome | Answer |
|---|---|
| Forecast returned | `200` |
| Model not in the supported list, or coordinates out of range | `422` |
| Provider unreachable, slow, error status, bad JSON | `502`, warning logged |

Sunrise/sunset are the provider's, so the calculations epic should reuse `ForecastDayDto` rather than compute them again.

### In the form (section 02 · Meteo)
An **HOURLY FORECAST** block below the wind-on-altitude table, in the same style: a model select, a **Fetch forecast**
button and one table per local day (time, °C, kt, direction, gust, CAPE, mm, %) with the day's sunrise and sunset in
its header. There is no mockup for the forecast table itself; the sunrise/sunset fields it feeds are in section 01 of
`nimbus-prep-form.html`.

- The hour of the planned flight (form date and time, read as the site's local time) is highlighted.
- A row where the model changes is preceded by "icon_eu from here (coarser than icon_d2)".
- **Use** on an hour fills wind, direction, CAPE and temperature (a missing value leaves its field alone) and the
  Sunrise and Sunset fields of section 01 for that day. Fetching alone changes no field.
- Provider down, or no data for the model: an empty state with the reason, never an error page; the form is unaffected.

## Wind series for the trajectory simulator (issue #47)

`GET /api/weather/wind-series?latitude=&longitude=&fromUtc=&toUtc=&model=` returns hourly wind snapshots between two UTC
hours: `WindSeriesDto { model, requestedFromUtc, requestedToUtc, fromUtc, toUtc, shortfallHours, gapHoursUtc[],
providerAvailable, snapshots[] }`. Each snapshot is `{ hourUtc, levels[] }` with the same levels as the wind profile
(#46): 10, 20, 50, 100, 150, 200 m above ground plus 950/925/900/850 hPa, lowest first. Data is Open-Meteo, knots, one
request for the whole window. `model` defaults to `OpenMeteo:ForecastModel` (`icon_seamless`), and only the models
the wind profile accepts (`WindProfileMapper.SupportedModels`) are allowed. Nothing is stored. No UI: it is for the simulator.

- **Clamped window.** Only hours from now up to the model's horizon exist: 96 h by default, 59 h for `icon_d2` and
  `ukmo_uk_deterministic_2km`, 56 h for `meteofrance_arome_france`. `fromUtc` / `toUtc` are what was served,
  `shortfallHours` is how many requested hours fall outside it (past hours count). A window wholly beyond the horizon
  is an empty series and the provider is not called.
- **Never null downstream.** `snapshots` and `gapHoursUtc` are always lists. A failed provider (down, slow, error
  status, bad JSON) is an empty series with `providerAvailable: false` and a logged warning, not a 502. This differs
  from `wind-profile`, where the pilot is waiting on a single answer.
- **Nulls are gaps, not zeros.** An hour where the model lacks any required level (or that the reply left out) is not
  a snapshot; it is listed in `gapHoursUtc`, so a snapshot is always complete.
- **Cache.** A successful fetch is cached per model, place (0.01°) and window for `OpenMeteo:SeriesCacheMinutes`
  (default 60, below the models' 3 to 6 h update cycles), so re-running a simulation does not refetch. The model is part
  of the key. A failed fetch is not cached. The cache is in memory, per API instance.

| Outcome | Answer |
|---|---|
| Series (possibly clamped, with gaps) | `200` |
| Provider failed | `200`, empty series, `providerAvailable: false` |
| Unsupported model, `toUtc` before `fromUtc`, window over 7 days, bad coordinates | `422` |

## Provider clients and failure policy (issue #48)

Every external provider sits behind its own named HTTP client, registered in `ExternalProviderRegistration`
(`Nimbus.Infrastructure/Http`): aviationweather.gov (`aviation-weather`), Open-Meteo (`open-meteo`, shared by the
forecast, wind-profile and wind-series clients), Overpass (`overpass`) and the static map tiles (`static-map-tiles`).
Overpass and the tiles have their client registered and configured but no caller yet; a later story uses
`IHttpClientFactory.CreateClient(ProviderClients.Overpass)` and gets the same policy.

Each provider's section (`AviationWeather`, `OpenMeteo`, `Overpass`, `StaticMapTiles`) has its own `BaseUrl` (https),
`TimeoutSeconds` (1 to 60) and `UserAgent`, validated at startup, plus a `Resilience` subsection:

| Key | Default | Meaning |
|---|---|---|
| `MaxRetries` | 2 | Extra attempts after the first, 0 to 5. 0 turns retrying off |
| `RetryBaseDelayMs` | 300 | First backoff; doubles each retry, with jitter |
| `BreakAfterFailures` | 5 | Failed calls within the window that (with a 50% failure ratio) open the circuit |
| `BreakDurationSeconds` | 30 | How long the circuit stays open before one probe call |
| `SamplingSeconds` | 30 | The breaker's failure window |

- **What is retried:** network errors, a per-attempt timeout, 408, 429 and 5xx, for body-less (GET) requests only. A 4xx
  other than 408/429 is our mistake and is returned at once. `TimeoutSeconds` is the timeout of **one attempt**, so the
  worst case for a call is `(MaxRetries + 1) x TimeoutSeconds` plus the backoff.
- **Circuit breaker:** one per *provider*, not per client, so the three Open-Meteo clients share it. While open, calls fail
  immediately without reaching the provider.
- **No exception crosses the layer boundary:** a call that still fails (circuit open, last attempt timed out) surfaces as an
  `HttpRequestException`, which the clients already turn into a failed `ProviderResult` plus a logged warning. A final 5xx comes back as a response and is
  handled the same way.
- **Forecast model:** `OpenMeteo:ForecastModel` (default `icon_seamless`) is validated at startup against the models that offer every
  wind level (`WindProfileMapper.SupportedModels`, which are all also forecast models), because the forecast, the wind profile and the
  simulation series share it. The wind profile now takes its default from it too.
- **API key:** `OpenMeteo:ApiKey` (set `OpenMeteo__ApiKey` in the API's environment, never in a committed file). Without a key the
  free, non-commercial endpoint (`api.open-meteo.com`) is used. With one, requests go to `customer-api.open-meteo.com` and carry
  `apikey=`. The Open-Meteo clients have HTTP-client logging switched off because the key is in the query string.

The free tier is non-commercial only. Rate limits and attribution wording were not checked and should be reviewed before any
commercial use.
