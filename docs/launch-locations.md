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
- Elevation (m AMSL): optional (nullable column); when given -500 to 9000, at most 2 decimals (`decimal(7,2)`). Blank is null.
- Airspace notes: optional free text, max 2000; blank becomes null.

## Order and delete
Sorted by name (case-insensitive) with the id as tie-break. Delete is a soft delete (`IsDeleted`) so flights that
reference a location keep resolving. There is no `Flight` entity yet, so deletion is not blocked.

## Account deletion
When an admin deletes a user account, all of that user's launch locations (including soft-deleted ones) are
hard-deleted (`UserAdministrationService.DeleteAsync`, covered in `AdminUsersTests`).

## UI (Settings > Locations)
Built to `Nimbus Locations Mockups.dc.html` (1A list, 1B empty, 2A/2B form, 3A delete, 4A mobile).
- List: columns NAME (airspace notes under the name), ICAO ("—" when empty), COORDINATES, ELEV. ("N m", "—" when
  unknown), edit/delete icons. Heading "My launch locations", subtitle "N LOCATIONS · DRIVE METAR/TAF, FORECAST & SUNRISE"
  and the footnote "NO ICAO CODE → METAR/TAF LOOKUP SKIPPED FOR THAT LOCATION". On mobile each row is a card
  (name, ICAO, three-dots menu, coordinates, "N m AMSL").
- Add/edit is a wide `Nimbus-modal` (860px) with two columns: fields left, Leaflet preview right (360px, zoom controls,
  header "PREVIEW · UPDATES AS YOU TYPE"). On mobile the map (150px) stacks on top and ICAO / elevation / latitude /
  longitude form a 2x2 grid. The preview (`components/location-map`, OpenStreetMap tiles, Leaflet loaded lazily) follows
  the latitude/longitude fields live.
- Without a valid pin the map shows a dashed placeholder: "NO PIN · ENTER COORDINATES", "NO PIN · LATITUDE OUT OF RANGE"
  or "NO PIN · LONGITUDE OUT OF RANGE". After a save attempt with errors a warning banner under the preview reads
  "Fix the highlighted fields before saving. The server validates these again."
- Not built: the reverse-geocoded place-name line under the preview in the mockup.
- Delete confirmation keeps the wording "removed from your launch locations. This can't be undone." (no flight-prep picker yet).
