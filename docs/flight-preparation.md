# Flight preparation schema (issue #31)

The flight-preparation dossier is the API's core entity: one `FlightPreparation` row per planned
(and, later, flown) flight, with four child collections. This doc exists so the schema's naming
and structure stay consistent as later issues (images upload, KML track, sharing, "mark as
flown", …) extend it.

## Why this exists

The original POC mixed English property names with Dutch column names, and stored uploaded image
bytes directly in the database. Nimbus fixes both:

- **One language.** Every property and column, across `FlightPreparation` and its children, is
  named in English. There is no Dutch anywhere in the schema (`Weelde`/`avondvlucht` only ever
  appear as **example data** in docs/tests, never as identifiers).
- **No file content in columns.** Images and the KML track are stored in object storage
  (`Nimbus.Infrastructure.Storage.S3ObjectStorageService`, see `docs/object-storage.md`); the
  database only ever holds the object-storage key, plus the content type needed to serve it back
  correctly. Uploading the bytes and cleaning up the backing object on delete belong to the
  features that own that upload (images: issue #51; KML track: issue #69) — this schema just
  reserves the reference columns.

## Entities

All entities live in `Nimbus.Domain.Entities`; mappings are in `AppDbContext.ConfigureFlightPreparation`.

### `FlightPreparation`

The dossier itself — an `OwnedEntity` (`OwnerId`, plus `Id`/`CreatedDate`/`UpdatedDate`/`IsDeleted`
from `BaseEntity`). Scoped to its owner the same way as every other entity in Nimbus (see
`docs/owner-scoping.md`); `IX_FlightPreparations_OwnerId` backs the "my flights" list query
(issue #40).

Covers all nine pre-flight sections as flat columns, grouped and commented in the entity itself:

| # | Section | Example columns |
|---|---|---|
| 01 | General | `FlightDate`, `FlightTime`, `SunriseTime`, `SunsetTime`, `FieldOwnerNotified` |
| 02 | Meteo | `WindSpeedKt`, `WindDirectionDeg`, `VisibilityKm`, `CapeJPerKg`, `Metar`, `Taf`, … |
| 03 | Airspace & NOTAMs | `NotamsChecked`, `AirspaceStructureNotes`, `AirspaceRestrictions`, `Obstacles` |
| 04 | Checklists | safety: `FirstAidAndExtinguisher`, `PassengerListFilled`, `FlightPlanFiled` (`ChecklistAnswer`); technical: `BurnerTested`, `GasCylindersChecked`, `BalloonVisualInspectionDone`, `MooringsChecked`, `InstrumentsChecked` |
| 05 | Load calculation | `EnvelopeWeightKg`, `MaxAltitudeFt` (AMSL), `LiftUnits`, `TotalLiftKg`, `LoadRemarks` (total weight, the lift-vs-weight flag and the Go/No-Go verdict are derived by the single server-side `IFlightAssessmentService` (issue #39), never stored and never recomputed by a surface; passengers are a child collection, see below) |
| 06 | Route & trajectory | `RouteDescription`, `ClimbRateMetersPerSecond`, `DescentRateMetersPerSecond`, `CruiseAltitudesFtCsv` |
| 07 | Balloon bulletin | `BulletinText` |
| 08 | OFP | `OfpOperatorName`, `OfpPicWeightKg`, `OfpFuelAvailableMinutes`, `OfpFuelRequiredMinutes`, `OfpLandingLocation` |
| 09 | Passenger briefing | `PaxBriefingText`, `PaxBriefingVersion`, `PaxBriefingIsCustom` |

Plus the flight-outcome fields populated by "mark as flown" (issue #68): `FlownAt`,
`ActualDurationMinutes`, `ActualLandingLocation`, `FuelConsumedLiters`, `HasVisibleDefects`,
`DefectsDescription`, `OutcomeRemarks`, and the KML track object reference
(`KmlTrackObjectKey` + `KmlTrackContentType`).

`BalloonId`, `PilotId` and `LaunchLocationId` reference the owner's own reference-data entities
(issues #24–26) by id only — no FK constraint, matching how those entities already reference
`OwnerId`. Referenced rows are soft-deleted, not hard-deleted, so a flight keeps resolving its
balloon/pilot/location even if the owner later "deletes" it from their reference-data lists.

### Child collections

All four cascade-delete with their parent `FlightPreparation` (`OnDelete(DeleteBehavior.Cascade)`
in `AppDbContext`, enforced by the database FK — see
`FlightPreparationSchemaTests.DeletingAFlightPreparation_CascadeDeletes_AllChildRows_LeavingNoOrphans`).
Each child is a plain `BaseEntity` (no separate owner column — ownership is always resolved
through the parent `FlightPreparation.OwnerId`).

| Entity | Table | Purpose | Composite index |
|---|---|---|---|
| `Passenger` | `Passengers` | Section 05 load-calculation passenger list (`Name`, `WeightKg`, `IsChild`, `NeedsAssistance`, `NeedsTransport`; `Order` preserves the pilot's ordering) | `(FlightPreparationId, Order)` |
| `FlightPreparationImage` | `FlightPreparationImages` | Image attached to one section | `(FlightPreparationId, Section, Order)` |
| `WindLevel` | `WindLevels` | Section 02 wind-on-altitude table row | `(FlightPreparationId, Order)` |
| `FlightPreparationShare` | `FlightPreparationShares` | Grants another user read access (issues #65/#66) | `(FlightPreparationId, SharedWithUserId)` unique, filtered on `IsDeleted = 0` |

`FlightPreparationImage.ObjectKey`/`ContentType` are the object-storage reference described above
— never bytes. `FlightPreparationImage.Section` uses the new `FlightPreparationSection` enum (see
below) so an image is always tagged to exactly one of the nine sections.

`FlightPreparationShare.SharedWithUserId` is, like `OwnerId`/`BalloonId`/etc., a plain id with no
FK constraint — the recipient is resolved against Identity at query time, not joined in the
database.

## Enums

- **`FlightPreparationSection`** (`Nimbus.Domain.Enums`) — the nine dossier sections
  (`General`, `Meteo`, `Airspace`, `Checklists`, `LoadCalculation`, `Route`, `Bulletin`, `Ofp`,
  `PaxBriefing`), stored as `nvarchar` via `HasConversion<string>()` so the database stays
  human-readable.
- **`FlightPreparationStatus`** (`Nimbus.Domain.Enums`) — `Planned` | `Flown`, also stored as
  `nvarchar`.

## Column naming convention

English, consistently, for every property and the column it maps to:

- `...Kt`/`...Kg`/`...Km`/`...Hpa`/`...Ft`/`...Deg`/`...C` unit suffixes on numeric columns
  (`WindSpeedKt`, `EnvelopeWeightKg`, `MaxAltitudeFt`, `WindDirectionDeg`, `TemperatureC`, …) so a
  column's unit is never ambiguous from its name alone.
- `...Json`/`...Csv` suffixes mark columns that hold structured text rather than a single scalar
  (`ChecklistItemsJson`, `CruiseAltitudesFtCsv`) — both are pragmatic flattenings documented inline
  on `FlightPreparation` (checklist items aren't owner-scoped reference data of their own yet;
  cruise altitudes are a small, fixed-size list).
- `...ObjectKey`/`...ContentType` pairs mark an object-storage reference (`KmlTrackObjectKey` +
  `KmlTrackContentType`, and `FlightPreparationImage.ObjectKey` + `ContentType`) — the same shape
  everywhere a file lives outside the database.
- Every child row carries its parent id as `FlightPreparationId` (never e.g. `FlightId` or a
  Dutch name), and a display-order column named plainly `Order`.

## Testing

- `Nimbus.Domain.Tests/Entities/FlightPreparationTests.cs` — entity defaults, that the KML track
  and image fields are reference-shaped (not bytes), and that `FlightPreparationSection` has
  exactly nine values.
- `Nimbus.Infrastructure.Tests/FlightPreparationSchemaTests.cs` — runs the real migration against
  SQL Server (Testcontainers) and proves: cascade delete leaves no orphaned child rows across all
  four children; the documented composite indexes exist; and listing/deleting by owner never
  crosses into another owner's flights (per-user isolation at the data layer).

## Section 1 form behaviour (issue #33)

- A new flight's date/time default to the next launch window (07:00 or 19:00 local, at least one hour ahead).
- Balloon, pilot, launch location and the optional landing location come from the owner's own reference data (the stores only return the signed-in user's rows).
- Picking a balloon on a not-yet-saved flight snapshots its envelope, basket, burner and cylinder weights into the load-calculation section; saved flights keep their snapshot.
- The selected launch location's coordinates are exposed as `launchCoordinates` on the form page for the weather and sunrise features.

## Sections 3-5 checks (issue #34)

- Section 03 keeps `NotamsChecked` (a bool, required to complete the section) plus the airspace/restrictions/obstacles notes.
- The checklist section holds eight typed checks instead of the old items list/JSON blob. The three safety checks use the `ChecklistAnswer` enum (`No` | `Yes` | `NotApplicable`, stored as a string column and serialised by name; replaces the POC's "JA"/"NEE"/"NVT"). The five technical checks are plain booleans because "not applicable" is never valid for them.
- Everything defaults to the unchecked state (`No` / `false`); nothing is pre-ticked. Migration `ChecklistSafetyAndTechnicalChecks` drops `ChecklistCompletedCount`/`ChecklistTotalCount`/`ChecklistItemsJson` (never written by any feature) and adds the new columns.

## Saving (issue #42)

`IFlightPreparationRepository` (`FlightPreparationRepository`) is the only write path for the
dossier. Every method takes the owner id and matches on id **and** owner; another user's flight
behaves like a missing one (`null`/`false`).

- **Transaction.** `AddAsync` and `UpdateAsync` run in an explicit database transaction and roll
  back on any failure — a half-saved dossier is impossible.
- **Detach / restore.** Before EF sees the entity, the flight's child collections (`Passengers`,
  `WindLevels`, `Images`, `Shares`) are emptied and each child's `FlightPreparation` navigation is
  nulled, so EF never cascades inserts through the graph. A `finally`-equivalent path restores them
  whether or not the save succeeded. On failure the generated ids, audit dates, child foreign keys
  and `Order` values are also rolled back, so the caller's object is exactly what it passed in
  (covered by `FlightPreparationRepositoryTests`).
- **Create.** The flight row is inserted first; the children are then re-parented to the generated
  id and inserted with `Order` = their position in the supplied list.
- **Update: replace, not diff.** Passengers and wind levels are value-like rows with no external
  identity (nothing references a passenger id), and the form always submits the whole ordered list.
  Update therefore hard-deletes all existing rows of those two collections (including soft-deleted
  ones, so none accumulate) and inserts the supplied list, renumbering `Order`. This is the one
  strategy for both collections. `Images` and `Shares` have their own lifecycles (object-storage
  upload #51, sharing #65/#66) and are **never** touched by a dossier save.
- **Columns the dossier save does not write.** `OwnerId`, `CreatedDate`, `IsDeleted`, the KML
  reference and `TrajectoryJson` are preserved from the stored row.
- **Patch operations.** `SetTrajectoryJsonAsync` (`TrajectoryJson`) and
  `SetKmlTrackAsync` (`KmlTrackObjectKey` + `KmlTrackContentType`) use `ExecuteUpdate` filtered on
  id and owner: a single statement, no row load, no change tracking. Each also bumps `UpdatedDate`
  in that same statement, since `ExecuteUpdate` bypasses the `SaveChanges` audit stamping.

## Saving and loading through the API

| Method | Route | Purpose |
|---|---|---|
| `GET` | `/api/flight-preparations/{id}` | Load a stored dossier (`FlightPreparationDto`: id, status, update stamp, `details`). |
| `POST` | `/api/flight-preparations` | Create. The caller becomes the owner and the flight starts as `Planned`. |
| `PUT` | `/api/flight-preparations/{id}` | Replace the form-owned fields, passengers and wind levels. |

- **Who may do what.** `GET /{id}` (the edit form's load) and `PUT` are for the owner **and an admin**
  (the flight-detail mockup gives an admin every owner action). For anyone else, including a viewer the
  flight is only shared with: `403` when the flight exists, `404` when it does not. The repository stays
  owner-scoped, so an admin's update is made *as the flight's owner* (the access check supplies the owner
  id) and ownership never moves to the admin. See "Flight detail view" for reading.
- **Known limit.** The form's balloon, pilot and location pickers list the *signed-in* user's reference
  data. An admin editing someone else's flight keeps the stored ids as long as those pickers are not
  touched, but cannot pick the owner's other balloons, and the API does not yet check that a referenced
  balloon, pilot or location belongs to the flight's owner.
- **`FlightPreparationRequestDto` is exactly what the form owns.** An update loads the stored row
  (read-only), applies the request onto it and hands it to `UpdateAsync`, so the columns the form
  does not own (status, flight outcome, simulation settings, precipitation, trajectory, KML) keep
  their stored values. Never build a fresh entity for an update: `UpdateAsync` writes every column.
- **Balloon-weight snapshot.** `BasketWeightKg`, `BurnerWeightKg` and `CylindersWeightKg` are copied
  from the balloon by the form when the balloon is picked on a new flight, and stored with the flight,
  so later edits to the balloon never change what a saved flight was assessed with. A saved flight
  keeps its snapshot even if its balloon is changed. The columns are nullable: rows saved without a
  snapshot fall back to the balloon's current weights in the flight list and in
  `FlightAssessmentInput.From`.
- **Not stored.** The landing-site pick on the form (`landingLocationId`) has no column and lives only
  in the local draft; `OfpLandingLocation` (free text) is stored.
- **Title.** The form sends `"<launch location> · <date>"`; a blank title falls back to
  `"Flight <date>"`.
- **Client.** Saving never throws away work: a failed request keeps the form dirty and the local
  autosave; a successful one removes the autosave and refreshes the flight list. A flight id the
  server does not know (404) is treated as an unsaved draft; any other load error blocks saving.

## Passenger briefing (issue #35)

Section 09 stores the briefing as one HTML string in `PaxBriefingText`, edited with `ngx-quill` (Quill 2).

- **Standard briefing.** A new flight starts with `PAX_BRIEFING_TEMPLATE_HTML`
  (`core/flight-preparations/pax-briefing-template.ts`): inflation, in flight, landing, after the
  flight. `PaxBriefingVersion` records which version of the wording a flight started from.
  Saved flights keep what is stored; the template is never applied over them.
- **Customized flag.** `PaxBriefingIsCustom` is derived, not ticked: it is true whenever the text
  differs from the template. "Reset to template" restores the text, version and flag and touches no
  other section.
- **Sanitised on the server.** Create and update run the text through `IRichTextSanitizer`
  (`RichTextSanitizer`, HtmlSanitizer with an allow-list) before it reaches the repository. Kept:
  `p br strong b em i u s h1-h3 ol ul li blockquote a`. Dropped: every attribute except a link's
  `href`, every other tag together with its content, and links that are not `http`, `https`,
  `mailto` or `tel`. Links get `rel="noopener noreferrer"`. A briefing with nothing visible (the
  editor's empty `<p><br></p>`) is stored as null. The raw HTML is capped at 20,000 characters.
- **Display.** Anything shown read-only goes through `Nimbus-rich-text-view`, which binds a plain
  `[innerHTML]` so Angular's sanitiser runs too. Do not use `quill-view-html` for display: it
  bypasses Angular's sanitiser unless told otherwise.
- **Editor formats** are limited to what the sanitiser keeps (headings, bold, italic, underline,
  strike, lists, quote, link), so nothing typed is silently dropped on save.
- **PDF export.** Not built yet. The export must render the stored (already sanitised) HTML; see the
  note in the PR for #35.

## Flight detail view (issue #41)

`GET /api/flight-preparations/{id}/detail` returns `FlightDetailDto`: the dossier (`details`, the same
shape the form saves), the single assessment (weights, lift margin, Go/No-Go), the flight's balloon,
pilot and launch-location names, the viewer's role and permissions, and the uploaded images with
short-lived download links. The page is `/flights/:id`; the flight list rows open it, and its Edit
action leads to the form.

- **Access is decided in `FlightDetailReader`, not the UI.** Owner, then admin (bypasses the share
  check), then an active share (a revoked share grants nothing). The reader answers "exists, but not
  yours" separately from "does not exist", so the API says `403` or `404`; a soft-deleted flight does
  not exist for anyone. No flight data leaves the reader for a viewer who may not see it.
- **Permissions** (`FlightPermissions`, computed per request): reading and exporting is open to every
  viewer; edit, delete, share and mark-as-flown belong to the owner and to an admin; a viewer the flight
  is only shared with gets none of them. The page shows an action only when its permission is true and
  the action exists; today that is Edit. Export PDF, Duplicate, share, mark as flown and delete get their
  buttons with their own stories (they only need to read `permissions`).
- **Layout.** Built to `nimbus-flight-detail.html` (in `../Nimbus mocups/`, one level above the repo): a
  header (title, a "date · time · balloon and type · pilot" line, a role badge for shared and admin
  views, a read-only note), a status strip (total, lift, margin, Go/No-Go) and one document card with
  grouped key/value rows, the checklists as tick lines, the passengers as a small table and the
  briefing in a box. Not built: the route map and the "after the flight" block (no trajectory or
  outcome data yet), and the Export PDF and Duplicate buttons. Differences kept on purpose: a back link,
  the balloon weight snapshot and max altitude rows, and English text (the mockup is Dutch).
- **Names are the owner's.** Balloon, pilot and location are resolved from the flight owner's reference
  data, including entries the owner has since soft-deleted, never from the viewer's own lists.
- **One assessment.** The strip and the margin come from `IFlightAssessmentService`, the
  same service the list uses, fed with the flight's weight snapshot (falling back to the balloon for
  rows without one). Like the list, it judges against the *viewer's* Go/No-Go thresholds.
- **403 and 404 go to the flight list.** The auth interceptor leaves a 403 on `GET /flight-preparations/{id}`
  and `/detail` to the pages (every other 403 still goes to the access-denied screen). The detail page
  sends the viewer to `/flights`; the edit form sends a viewer who may only read to `/flights/:id`.
- **Rendering.** `buildDetailSections` (pure) turns the DTO into the nine sections in form order; the
  page only displays them. Empty values show a dash. The briefing goes through `Nimbus-rich-text-view`
  (Angular's sanitiser). An image whose link cannot be made is left out rather than failing the view.

## Deleting a flight (issue #43)

`DELETE /api/flight-preparations/{id}` answers `204`. The page offers it on the flight detail view,
only when `permissions.canDelete` is true, behind a dialog that names the flight and says it is
permanent.

- **Decision: a hard delete.** The row is removed, not flagged. The rest of the app soft-deletes
  reference data so that old flights keep resolving a balloon or location; a flight is the thing being
  resolved, so nothing needs it afterwards. Two reasons not to keep a hidden copy: passenger names and
  weights are personal data that should be gone when the pilot says so, and a flight's images and KML
  track are files in object storage that would otherwise stay for good. The confirmation therefore says
  plainly: *This permanently deletes the flight with its passengers, wind levels, images and shares.
  This can't be undone.* (Trade-off accepted: a flight deleted by mistake cannot be recovered.)
- **What goes with it.** Passengers, wind levels, images and shares are removed by the database's
  `ON DELETE CASCADE` foreign keys, soft-deleted rows included (a test asserts every table is empty for
  the flight). Revoking access is therefore automatic: shared viewers' links stop working.
- **Files.** The repository returns the object keys the flight referenced; the handler then removes the
  image files (`FlightImages` bucket) and the KML track (`FlightTracks`) best effort. The row is the
  source of truth, so a storage failure neither blocks nor undoes the delete: the file is logged as
  orphaned (`Flight {FlightId} is deleted but its file {Key} ... is orphaned`) and the rest are still
  tried. A malformed stored key is skipped with a warning.
- **Who.** The owner and an admin (an admin deletes on the owner's behalf: the owner-scoped repository
  is asked as the flight's owner). Anyone else who can see the flight gets `403` and the attempt is
  logged as a warning (user, flight, viewer role) before anything is touched; a flight that does not
  exist is `404` and is not logged as an attempt. A successful delete is logged at information level
  with who deleted it and in which role.
- **Client.** The dialog stays open with a message when the server refuses or fails (nothing was
  removed), a repeated click cannot send a second request, a `404` (someone deleted it first) counts as
  done, and the flight's local autosave draft is dropped so it cannot resurface in the form.

## Flight list (issue #40)

`GET /api/flight-preparations?filter=&sort=&descending=&page=&pageSize=` feeds the Flights overview
(`pages/flight-overview`, state in `FlightListStore`).

- **Visibility.** The caller's own flights plus flights with an active (non-revoked) share to the caller;
  an admin sees every flight. The user id and admin flag come from the auth context
  (`ICurrentUserService`), never from query parameters.
- **Filters** (`FlightListFilter`): `All`, `Flown`, `NotFlown`, `SharedWithMe` (flights owned by someone else).
  **Sort** (`FlightListSort`): `Date` (default, newest first), `Balloon`, `Location`, `Status`, with
  `descending`. `pageSize` is 1-100 (validated), default 20.
- **SQL only.** `FlightListReader` (Infrastructure) runs one `COUNT`, one paged projection (balloon
  registration/weights, location name and pilot weight resolved by correlated sub-selects) and one query for
  the page's passenger weights: three commands regardless of row count, no `FlightPreparation` entities and no
  child collections materialised (`FlightListReaderTests` seeds 300+ flights and asserts the command count,
  that nothing is tracked and that wide columns are not selected). Soft-deleted balloons/locations/pilots still
  resolve, as for the detail view.
- **Verdict.** Each row's `goNoGoVerdict` comes from one `IFlightAssessmentService.AssessManyAsync` call for
  the page (limits resolved once, the viewer's own thresholds); the list never recomputes it.
- **Owner names.** `IUserDirectory.GetDisplayNamesAsync` resolves all foreign owners on the page in one
  `WHERE Id IN (...)` query; own flights carry no owner name.
- **Index.** Migration `FlightListSharedWithUserIndex` adds a filtered index on
  `FlightPreparationShares.SharedWithUserId` for the share lookup.
