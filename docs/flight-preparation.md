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
| 05 | Load calculation | `EnvelopeWeightKg`, `MaxAltitudeFt` (AMSL), `LiftUnits`, `TotalLiftKg`, `LoadRemarks` (total weight and the lift-vs-weight verdict are derived in the UI, never stored; passengers are a child collection, see below) |
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
