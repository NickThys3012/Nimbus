using Nimbus.Domain.Enums;
namespace Nimbus.Contracts.DTOs.Features.FlightPreparations;

/// <summary>
///     The columns the dossier form owns (sections 1-9). Anything the form does not own (flight outcome, simulation
///     settings, trajectory, KML, shares, images) is never part of this request and survives a save untouched.
///     The owner is never part of a request.
/// </summary>
public record FlightPreparationRequestDto
{
    /// <summary>Free-text label for lists; blank falls back to the flight date.</summary>
    public string? Title { get; init; }

    public required Guid BalloonId { get; init; }
    public required Guid PilotId { get; init; }
    public required Guid LaunchLocationId { get; init; }

    // Section 1 — general
    public required DateOnly FlightDate { get; init; }
    public required TimeOnly FlightTime { get; init; }
    public TimeOnly? SunriseTime { get; init; }
    public TimeOnly? SunsetTime { get; init; }
    public bool FieldOwnerNotified { get; init; }

    // Section 2 — meteo
    public decimal? WindSpeedKt { get; init; }
    public int? WindDirectionDeg { get; init; }
    public decimal? VisibilityKm { get; init; }
    public decimal? CapeJPerKg { get; init; }
    public decimal? TemperatureC { get; init; }
    public decimal? DewPointC { get; init; }
    public decimal? QnhHpa { get; init; }
    public string? Metar { get; init; }
    public string? Taf { get; init; }
    public IReadOnlyList<WindLevelDto> WindLevels { get; init; } = [];

    // Section 3 — airspace
    public bool NotamsChecked { get; init; }
    public string? AirspaceStructureNotes { get; init; }
    public string? AirspaceRestrictions { get; init; }
    public string? Obstacles { get; init; }

    // Section 4 — checklists
    public ChecklistAnswer FirstAidAndExtinguisher { get; init; }
    public ChecklistAnswer PassengerListFilled { get; init; }
    public ChecklistAnswer FlightPlanFiled { get; init; }
    public bool BurnerTested { get; init; }
    public bool GasCylindersChecked { get; init; }
    public bool BalloonVisualInspectionDone { get; init; }
    public bool MooringsChecked { get; init; }
    public bool InstrumentsChecked { get; init; }

    // Section 5 — load calculation (basket, burner and cylinder weights come from the balloon, never from the flight)
    public decimal? EnvelopeWeightKg { get; init; }
    public int? MaxAltitudeFt { get; init; }
    public decimal? LiftUnits { get; init; }
    public decimal? TotalLiftKg { get; init; }
    public string? LoadRemarks { get; init; }
    public IReadOnlyList<PassengerDto> Passengers { get; init; } = [];

    // Section 6 — route
    public string? RouteDescription { get; init; }
    public decimal? ClimbRateMetersPerSecond { get; init; }
    public decimal? DescentRateMetersPerSecond { get; init; }
    public string? CruiseAltitudesFtCsv { get; init; }

    // Section 7 — bulletin
    public string? BulletinText { get; init; }

    // Section 8 — OFP
    public string? OfpOperatorName { get; init; }
    public decimal? OfpPicWeightKg { get; init; }
    public int? OfpFuelAvailableMinutes { get; init; }
    public int? OfpFuelRequiredMinutes { get; init; }
    public string? OfpLandingLocation { get; init; }

    // Section 9 — passenger briefing
    public string? PaxBriefingText { get; init; }
    public string? PaxBriefingVersion { get; init; }
    public bool PaxBriefingIsCustom { get; init; }
}

public record WindLevelDto(int AltitudeM, int DirectionDeg, decimal SpeedKt, decimal TemperatureC);

public record PassengerDto(string Name, decimal WeightKg, bool NeedsAssistance, bool IsChild, bool NeedsTransport);

/// <summary>A stored dossier: its identity and status plus the form-owned fields.</summary>
public record FlightPreparationDto(Guid Id, string Status, DateTime? UpdatedDate, FlightPreparationRequestDto Details);
