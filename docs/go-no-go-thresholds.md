# Go/No-Go thresholds (issue #27)

A user sets personal wind, visibility and CAPE limits under **Settings > Go/No-Go**, so the traffic light reflects their own minima.

## Model
`GoNoGoThreshold` (table `GoNoGoThresholds`): one row per user (`OwnerId`, unique where not null), six decimal(7,2) values:
wind yellow/red (kt), visibility yellow/red (km), CAPE yellow/red (J/kg). Not soft-deleted: resetting removes the row.

## Defaults
Built in: wind 10/15, visibility 5/3, CAPE 300/500. A row with `OwnerId = null` is the **global default**: a read-only
fallback used for users without their own row (falls back to the built-in values when absent). It is only read by
`GetGlobalDefaultAsync`; no user endpoint can create, update or delete it (writes always look up the caller's own row).
Nothing seeds it; it can only be managed by a DBA/migration.

## API (`api/go-no-go-thresholds`, `[Authorize]`, no id in any route)
- `GET`: effective values of the caller plus `isCustom` (false when defaults apply).
- `PUT`: upsert the caller's own row (updated in place, primary key and owner never reassigned); 422 on invalid input.
- `DELETE`: reset to defaults: removes the caller's own row, returns the effective defaults (idempotent).

## Validation (FluentValidation, mirrored in the Angular form)
Each value > 0 and <= 100 (wind, visibility) / 10000 (CAPE), max 2 decimals. Red must be strictly stricter than yellow:
wind and CAPE red > yellow, visibility red < yellow. Errors are keyed `Thresholds.<Property>`.

## Per-user isolation
Owner comes from the auth principal only; the request DTO has no owner/id. Handlers query by `OwnerId == currentUser`.
Deleting an account hard-deletes its row (`UserAdministrationService.DeleteAsync`). Tests: `GoNoGoThresholdsTests` (Application + Api).

## UI
Settings tab "Go/No-Go" (tabs, not a separate route): parameter table with caution/no-go inputs, USING DEFAULTS / CUSTOM badge,
preview chip (12 kt, 4 km, 250 J/kg), inconsistency banner and per-field errors, Save (disabled while invalid), and Reset to defaults with a confirm dialog.
