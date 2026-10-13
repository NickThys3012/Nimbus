# OFP settings (issue #28)

A user sets the equipment weight counted per passenger under **Settings > OFP** (default 7 kg).

## Model
`OfpSettings` (table `OfpSettings`): one row per user (`OwnerId`, unique), `EquipmentWeightPerPassengerKg` decimal(5,2).
Not soft-deleted: resetting removes the row. The default (7 kg) is built in (`OfpSettingsDefaults`), there is no global row.

## Where the value is used
Only in the OFP total-weight formula (`OfpWeightCalculator`): total = basic weight + passengers x equipment weight.
The basic weight check never uses it. The value is a parameter, so a flight snapshots the value in effect when it is
prepared; changing the setting later never re-computes already-flown flights (their recorded OFP figures stay).
The UI says so ("Applies to flights you prepare from now on").

## API (`api/ofp-settings`, `[Authorize]`, no id in any route)
- `GET`: effective value of the caller plus `isCustom` (false when the default applies).
- `PUT`: upsert the caller's own row (updated in place); 422 on invalid input.
- `DELETE`: reset to default: removes the caller's own row, returns the default (idempotent).

## Validation (FluentValidation, mirrored in the Angular form)
0 to 100 kg inclusive (0 allowed), at most 2 decimals.

## Per-user isolation
Owner comes from the auth principal only; the request DTO has no owner/id. Handlers query by `OwnerId == currentUser`.
Deleting an account hard-deletes its row. Tests: `OfpSettingsTests` (Application + Api) incl. user A vs user B.

## UI
Settings tab "OFP": weight input, worked example (8 passengers), DEFAULT / CUSTOM / UNSAVED badge, info notice about
flown flights when changed, inline validation, Save (disabled while invalid), Reset to default with confirm dialog.
