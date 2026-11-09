using Nimbus.Domain.Entities.Base;
using Nimbus.Domain.Enums;
namespace Nimbus.Domain.Entities;

/// <summary>
///     The flight-preparation dossier (issue #31): the nine pre-flight sections plus, once the
///     flight has happened, the flight-outcome fields recorded by "mark as flown" (issue #68).
///     Owned by one user; every child collection (<see cref="Passengers" />, <see cref="Images" />,
///     <see cref="WindLevels" />, <see cref="Shares" />) cascade-deletes with the parent row.
/// </summary>
/// <remarks>
///     <para>
///         Column naming is English throughout — including enum-backed columns — to fix the POC's
///         mix of English property names and Dutch column names (see docs/flight-preparation.md).
///     </para>
///     <para>
///         <see cref="BalloonId" />, <see cref="PilotId" /> and <see cref="LaunchLocationId" />
///         reference other owned reference-data entities by id only (no FK constraint, same
///         convention those entities already document): deleting the referenced balloon/pilot/
///         location is a soft delete, so a flight that references it keeps resolving.
///     </para>
///     <para>
///         The KML track (issue #69) is stored as an object-storage reference
///         (<see cref="KmlTrackObjectKey" /> + <see cref="KmlTrackContentType" />), never as file
///         content in a column — same convention as <see cref="FlightPreparationImage" />.
///     </para>
/// </remarks>
public class FlightPreparation : OwnedEntity
{
    /// <summary>Free-text label shown in lists, e.g. "Weelde · avondvlucht".</summary>
    public string Title { get; set; } = "";

    public FlightPreparationStatus Status { get; set; } = FlightPreparationStatus.Planned;

    /// <summary>Reference-data id only; see remarks on this type.</summary>
    public Guid? BalloonId { get; set; }

    /// <summary>Reference-data id only; see remarks on this type.</summary>
    public Guid? PilotId { get; set; }

    /// <summary>Reference-data id only; see remarks on this type.</summary>
    public Guid? LaunchLocationId { get; set; }

    // ----- Section 01 — general -----
    public DateOnly FlightDate { get; set; }
    public TimeOnly FlightTime { get; set; }
    public TimeOnly? SunriseTime { get; set; }
    public TimeOnly? SunsetTime { get; set; }
    public bool FieldOwnerNotified { get; set; }

    // ----- Section 02 — meteo -----
    public decimal? WindSpeedKt { get; set; }
    public int? WindDirectionDeg { get; set; }
    public decimal? VisibilityKm { get; set; }
    public decimal? CapeJPerKg { get; set; }
    public decimal? TemperatureC { get; set; }
    public decimal? DewPointC { get; set; }
    public decimal? QnhHpa { get; set; }
    public string? Precipitation { get; set; }
    public string? Metar { get; set; }
    public string? Taf { get; set; }

    // ----- Section 03 — airspace & NOTAMs -----
    public bool NotamsChecked { get; set; }
    public string? AirspaceStructureNotes { get; set; }
    public string? AirspaceRestrictions { get; set; }
    public string? Obstacles { get; set; }

    // ----- Section 04 — checklists: safety checks (issue #34) -----
    // Three-state answers; the default is No (unchecked), never a pre-ticked box.
    public ChecklistAnswer FirstAidAndExtinguisher { get; set; } = ChecklistAnswer.No;
    public ChecklistAnswer PassengerListFilled { get; set; } = ChecklistAnswer.No;
    public ChecklistAnswer FlightPlanFiled { get; set; } = ChecklistAnswer.No;

    // ----- Section 04 — checklists: technical checks (issue #34) -----
    // Always applicable, so plain booleans that default to unchecked.
    public bool BurnerTested { get; set; }
    public bool GasCylindersChecked { get; set; }
    public bool BalloonVisualInspectionDone { get; set; }
    public bool MooringsChecked { get; set; }
    public bool InstrumentsChecked { get; set; }

    // ----- Section 05 — load calculation -----
    public decimal? EnvelopeWeightKg { get; set; }

    /// <summary>
    ///     Basket, burner and cylinder weights copied from the balloon when the flight is prepared, so later edits to the
    ///     balloon never change what this flight was assessed with. Null on rows saved before the snapshot existed; readers
    ///     then fall back to the balloon's current weights.
    /// </summary>
    public decimal? BasketWeightKg { get; set; }

    public decimal? BurnerWeightKg { get; set; }
    public decimal? CylindersWeightKg { get; set; }
    public int? MaxAltitudeFt { get; set; }

    /// <summary>Number of lift units entered by the pilot. Total weight and the lift-vs-weight verdict are derived, never stored.</summary>
    public decimal? LiftUnits { get; set; }

    public decimal? TotalLiftKg { get; set; }
    public string? LoadRemarks { get; set; }

    // ----- Section 06 — route & trajectory -----
    public string? RouteDescription { get; set; }
    public decimal? ClimbRateMetersPerSecond { get; set; }
    public decimal? DescentRateMetersPerSecond { get; set; }
    public int? SimulationDurationMinutes { get; set; }
    public int? SimulationStepSeconds { get; set; }

    /// <summary>Up to five planned cruise altitudes (ft), comma-separated, in display order.</summary>
    public string? CruiseAltitudesFtCsv { get; set; }

    /// <summary>
    ///     The simulated trajectory as a JSON document (high-frequency autosave target). Written only
    ///     by <c>IFlightPreparationRepository.SetTrajectoryJsonAsync</c>; the full dossier save never
    ///     touches it (see docs/flight-preparation.md, "Saving").
    /// </summary>
    public string? TrajectoryJson { get; set; }

    // ----- Section 07 — balloon bulletin -----
    public string? BulletinText { get; set; }

    // ----- Section 08 — OFP -----
    public string? OfpOperatorName { get; set; }
    public decimal? OfpPicWeightKg { get; set; }
    public int? OfpFuelAvailableMinutes { get; set; }
    public int? OfpFuelRequiredMinutes { get; set; }
    public string? OfpLandingLocation { get; set; }
    public DateTime? OfpPlannedLandingTime { get; set; }
    public bool OfpVisibleDefectsChecked { get; set; }

    // ----- Section 09 — passenger briefing -----
    public string? PaxBriefingText { get; set; }
    public string? PaxBriefingVersion { get; set; }
    public bool PaxBriefingIsCustom { get; set; }

    // ----- Flight-outcome fields ("mark as flown", issue #68) -----
    public DateTime? FlownAt { get; set; }
    public int? ActualDurationMinutes { get; set; }
    public string? ActualLandingLocation { get; set; }
    public decimal? FuelConsumedLiters { get; set; }
    public bool? HasVisibleDefects { get; set; }
    public string? DefectsDescription { get; set; }
    public string? OutcomeRemarks { get; set; }

    /// <summary>Object-storage key (see docs/object-storage.md); null until a KML track is uploaded.</summary>
    public string? KmlTrackObjectKey { get; set; }
    public string? KmlTrackContentType { get; set; }

    public ICollection<Passenger> Passengers { get; set; } = [];
    public ICollection<FlightPreparationImage> Images { get; set; } = [];
    public ICollection<WindLevel> WindLevels { get; set; } = [];
    public ICollection<FlightPreparationShare> Shares { get; set; } = [];
}
