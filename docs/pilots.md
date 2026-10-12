# Pilots (issue #25)

A user maintains the pilots (name and weight) they fly with under **Settings > Pilots**, so
weight-and-balance in flight preparation uses the right figure for whoever is flying.

## API
`api/pilots` (default `[Authorize]` policy): `GET` list, `GET {id}`, `POST` (201), `PUT {id}`, `DELETE {id}` (204).

## Per-user isolation
- `Pilot` is an `OwnedEntity`; the owner is stamped from the authenticated user, never from the request.
- Handlers always act as `new Caller(userId, isAdmin: false)`: "my pilots" is the caller's own list, even for admins.
- Another user's id answers 404 on get, update and delete (same as a missing id).

## Fields and validation (server side, FluentValidation)
- Name: required, not blank, max 100; stored trimmed. Not unique (two pilots may share a name).
- Weight (kg): required, **20 to 250 inclusive**, at most 2 decimals (column `decimal(5,2)`). The Angular form mirrors the range.

## Order
The list is sorted by name (case-insensitive) with the id as tie-break, so the order is stable between calls.
The flight-preparation picker should use `GET api/pilots` as is; it needs no ordering of its own.

## Delete
Soft delete (`IsDeleted`, the `BaseEntity` convention) so flights referencing a pilot keep resolving.
There is no `Flight` entity yet, so deletion is not blocked. When the flight feature lands, decide whether to
block deleting a used pilot the way balloons do (see [balloons.md](balloons.md)).

## Account deletion
When an admin deletes a user account, all of that user's pilots (including soft-deleted ones) are hard-deleted
(`UserAdministrationService.DeleteAsync`, covered in `AdminUsersTests`).
