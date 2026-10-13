# My profile (issue #29)

A user stores an **operator name** and their own **PIC weight (kg)** under **Settings > Profile**. Both are optional and meant to
pre-fill every new flight preparation.

## Model
`UserProfile` (table `UserProfiles`): one row per user (`OwnerId`, unique), `OperatorName` (max 200, blank stored as null),
`PicWeightKg` decimal(5,2) nullable. The row is created on first save; no row means "empty profile".

## API (`api/profile`, `[Authorize]`, no id in any route)
- `GET`: the caller's profile (both values null when unset).
- `PUT`: upsert the caller's own row (updated in place); 422 on invalid input (errors keyed `Profile.<Property>`).

Validation: weight > 0 and <= 500, max 2 decimals; name <= 200 characters. Mirrored in the Angular form.

## Snapshot onto flights
Flight preparations do not exist yet. When they are added, creation must **copy** `OperatorName` / `PicWeightKg` from
`IUserProfileRepository.GetByOwnerAsync(currentUser)` into the flight (missing values stay empty and never block creation).
The flight stores its own copy, so later profile edits never change existing flights.

## Per-user isolation
Owner comes from the auth principal only; the request DTO has no owner/id. Handlers query by `OwnerId == currentUser`.
Deleting an account hard-deletes its row (`UserAdministrationService.DeleteAsync`). Tests: `ProfileTests` (Application + Api).
