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

- **Owner only.** Another user's flight answers 404, same as a missing one. Opening a flight
  that is shared with you is not supported yet; the list shows shared flights but `GET` does not.
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
