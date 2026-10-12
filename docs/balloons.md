# Balloons (issue #24)

A pilot maintains the balloons they fly under **Settings > My balloons**, so a
flight can pick one instead of retyping its details.

## API
`api/balloons` (default `[Authorize]` policy): `GET` list, `GET {id}`, `POST` (201),
`PUT {id}`, `DELETE {id}` (204).

## Per-user isolation
- `Balloon` is an `OwnedEntity`; the owner is stamped from the authenticated user, never from the request.
- Handlers always act as `new Caller(userId, isAdmin: false)`: "my balloons" is the caller's own list, even for admins.
- Another user's id answers 404 on get, update and delete (same as a missing id).

## Fields
Registration (required, max 20, stored trimmed and upper-case), type (optional, max 100; stored as an empty string when omitted), envelope volume (m3),
max envelope temperature (C) and OFP reference weights (kg) for envelope, basket, burner and cylinders.

**Empty weight is derived**: `Balloon.EmptyWeightKg` is the sum of the four reference weights. It is
not stored and not part of any request; the UI shows it read-only and recalculates it live.

## Registration uniqueness
Unique per owner, case-insensitive. Enforced by `BalloonService` (409 Conflict) and, for concurrent
requests, by a filtered unique index on `(OwnerId, Registration) WHERE IsDeleted = 0`
(also mapped to 409). Different owners may use the same registration.

## Deleting a balloon that a flight uses
**Decision: blocked.** (Chosen in the issue #24 mockups, frames 3A/3B.) A balloon used by at least one flight cannot be
deleted; deleting it would break the logbook entries of those flights.
- `GET api/balloons/{id}/usage` returns `{ flightCount, recentFlights: [{ label, date }] }` (owner-scoped, 404 for foreign ids).
  The UI fetches it before deleting: 0 flights shows the confirm dialog, otherwise the "can't be deleted" dialog.
- `DELETE api/balloons/{id}` re-checks server-side and answers **409 Conflict** (`InUseException`) when flights use it.
  The balloon is left untouched. The user is told to edit the balloon instead.
- A balloon that is not used is soft-deleted (`IsDeleted`, the global `BaseEntity` convention): it disappears from
  "My balloons" and its registration is free for reuse (the unique index ignores deleted rows).

### Hook for the flight feature
There is no `Flight` entity yet, so `IBalloonUsageReader` (Domain) is bound to `NoFlightsBalloonUsageReader`
(Infrastructure), which reports no usage. **The flight feature must replace it** with an implementation over the Flight
table, filtered by owner, returning the flight count and the most recent flights (label such as "VR 08 · Weelde" and date).
The block, the endpoint and the UI then work unchanged. Until then the 409 path is covered with a fake reader
(`BalloonTests`, `BalloonsTests.Delete_WhenFlightsUseTheBalloon_Is409_AndBalloonRetained`).

## Account deletion
Balloons are personal data: when an admin deletes a user account, all of that user's balloons (including soft-deleted
ones) are hard-deleted with it (`UserAdministrationService.DeleteAsync`, covered in `AdminUsersTests`).
