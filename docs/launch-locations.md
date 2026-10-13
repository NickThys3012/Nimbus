# Launch locations (issue #26)

A user maintains the fields they launch from under **Settings > Launch locations**. The coordinates drive
weather and sunrise calculations, and the ICAO code (when present) drives the METAR/TAF lookup.

## API
`api/launch-locations` (default `[Authorize]` policy): `GET` list, `GET {id}`, `POST` (201), `PUT {id}`, `DELETE {id}` (204).

## Per-user isolation
- `LaunchLocation` is an `OwnedEntity`; the owner is stamped from the authenticated user, never from the request.
- Handlers always act as `new Caller(userId, isAdmin: false)`: "my launch locations" is the caller's own list, even for admins.
- Another user's id answers 404 on get, update and delete (same as a missing id).

## Fields and validation (server side, FluentValidation)
- Name: required, not blank, max 100; stored trimmed. Not unique.
- ICAO code: optional; when present exactly four letters or digits, stored upper case. Blank becomes null.
- Latitude -90 to 90 and longitude -180 to 180, decimal degrees, at most 6 decimals (`decimal(9,6)`).
- Elevation (m AMSL): required, -500 to 9000, at most 2 decimals (`decimal(7,2)`).
- Airspace notes: optional free text, max 2000; blank becomes null.

## Order and delete
Sorted by name (case-insensitive) with the id as tie-break. Delete is a soft delete (`IsDeleted`) so flights that
reference a location keep resolving. There is no `Flight` entity yet, so deletion is not blocked.

## Account deletion
When an admin deletes a user account, all of that user's launch locations (including soft-deleted ones) are
hard-deleted (`UserAdministrationService.DeleteAsync`, covered in `AdminUsersTests`).

## UI
Table-like list (name with ICAO badge, coordinates, elevation, edit/delete), add/edit in a modal dialog.
The dialog has a Leaflet preview map (`components/location-map`, OpenStreetMap tiles, Leaflet loaded lazily)
that follows the latitude/longitude fields live and is replaced by a hint while they are empty or out of range.
No mockup exists for this screen; it follows the pilots page.
