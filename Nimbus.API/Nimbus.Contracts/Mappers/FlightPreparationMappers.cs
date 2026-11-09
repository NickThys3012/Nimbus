using Nimbus.Contracts.DTOs.Features.FlightPreparations;
using Nimbus.Domain.Entities;
namespace Nimbus.Contracts.Mappers;

public static class FlightPreparationMappers
{
    public const int MaxTitleLength = 200;

    /// <summary>
    ///     Writes the form-owned columns onto <paramref name="f" /> and replaces its passengers and wind levels with the
    ///     supplied, ordered lists. Columns the form does not own are left exactly as they are, so applying a request to a
    ///     stored flight never wipes the outcome or simulation data.
    /// </summary>
    public static FlightPreparation ApplyTo(this FlightPreparationRequestDto r, FlightPreparation f)
    {
        f.Title = Text(r.Title) ?? $"Flight {r.FlightDate:yyyy-MM-dd}";
        f.BalloonId = r.BalloonId;
        f.PilotId = r.PilotId;
        f.LaunchLocationId = r.LaunchLocationId;

        f.FlightDate = r.FlightDate;
        f.FlightTime = r.FlightTime;
        f.SunriseTime = r.SunriseTime;
        f.SunsetTime = r.SunsetTime;
        f.FieldOwnerNotified = r.FieldOwnerNotified;

        f.WindSpeedKt = r.WindSpeedKt;
        f.WindDirectionDeg = r.WindDirectionDeg;
        f.VisibilityKm = r.VisibilityKm;
        f.CapeJPerKg = r.CapeJPerKg;
        f.TemperatureC = r.TemperatureC;
        f.DewPointC = r.DewPointC;
        f.QnhHpa = r.QnhHpa;
        f.Metar = Text(r.Metar);
        f.Taf = Text(r.Taf);

        f.NotamsChecked = r.NotamsChecked;
        f.AirspaceStructureNotes = Text(r.AirspaceStructureNotes);
        f.AirspaceRestrictions = Text(r.AirspaceRestrictions);
        f.Obstacles = Text(r.Obstacles);

        f.FirstAidAndExtinguisher = r.FirstAidAndExtinguisher;
        f.PassengerListFilled = r.PassengerListFilled;
        f.FlightPlanFiled = r.FlightPlanFiled;
        f.BurnerTested = r.BurnerTested;
        f.GasCylindersChecked = r.GasCylindersChecked;
        f.BalloonVisualInspectionDone = r.BalloonVisualInspectionDone;
        f.MooringsChecked = r.MooringsChecked;
        f.InstrumentsChecked = r.InstrumentsChecked;

        f.EnvelopeWeightKg = r.EnvelopeWeightKg;
        f.BasketWeightKg = r.BasketWeightKg;
        f.BurnerWeightKg = r.BurnerWeightKg;
        f.CylindersWeightKg = r.CylindersWeightKg;
        f.MaxAltitudeFt = r.MaxAltitudeFt;
        f.LiftUnits = r.LiftUnits;
        f.TotalLiftKg = r.TotalLiftKg;
        f.LoadRemarks = Text(r.LoadRemarks);

        f.RouteDescription = Text(r.RouteDescription);
        f.ClimbRateMetersPerSecond = r.ClimbRateMetersPerSecond;
        f.DescentRateMetersPerSecond = r.DescentRateMetersPerSecond;
        f.CruiseAltitudesFtCsv = Text(r.CruiseAltitudesFtCsv);

        f.BulletinText = Text(r.BulletinText);

        f.OfpOperatorName = Text(r.OfpOperatorName);
        f.OfpPicWeightKg = r.OfpPicWeightKg;
        f.OfpFuelAvailableMinutes = r.OfpFuelAvailableMinutes;
        f.OfpFuelRequiredMinutes = r.OfpFuelRequiredMinutes;
        f.OfpLandingLocation = Text(r.OfpLandingLocation);

        f.PaxBriefingText = Text(r.PaxBriefingText);
        f.PaxBriefingVersion = Text(r.PaxBriefingVersion);
        f.PaxBriefingIsCustom = r.PaxBriefingIsCustom;

        f.WindLevels = r.WindLevels
            .Select(w => new WindLevel { AltitudeM = w.AltitudeM, DirectionDeg = w.DirectionDeg, SpeedKt = w.SpeedKt, TemperatureC = w.TemperatureC })
            .ToList();
        f.Passengers = r.Passengers
            .Select(p => new Passenger { Name = p.Name.Trim(), WeightKg = p.WeightKg, NeedsAssistance = p.NeedsAssistance, IsChild = p.IsChild, NeedsTransport = p.NeedsTransport })
            .ToList();
        return f;
    }

    public static FlightPreparationDto MapToDto(this FlightPreparation f) =>
        new(f.Id, f.Status.ToString(), f.UpdatedDate, f.MapToRequestDto());

    public static FlightPreparationRequestDto MapToRequestDto(this FlightPreparation f) => new()
    {
        Title = f.Title,
        BalloonId = f.BalloonId ?? Guid.Empty,
        PilotId = f.PilotId ?? Guid.Empty,
        LaunchLocationId = f.LaunchLocationId ?? Guid.Empty,
        FlightDate = f.FlightDate,
        FlightTime = f.FlightTime,
        SunriseTime = f.SunriseTime,
        SunsetTime = f.SunsetTime,
        FieldOwnerNotified = f.FieldOwnerNotified,
        WindSpeedKt = f.WindSpeedKt,
        WindDirectionDeg = f.WindDirectionDeg,
        VisibilityKm = f.VisibilityKm,
        CapeJPerKg = f.CapeJPerKg,
        TemperatureC = f.TemperatureC,
        DewPointC = f.DewPointC,
        QnhHpa = f.QnhHpa,
        Metar = f.Metar,
        Taf = f.Taf,
        WindLevels = f.WindLevels.OrderBy(w => w.Order).Select(w => new WindLevelDto(w.AltitudeM, w.DirectionDeg, w.SpeedKt, w.TemperatureC)).ToList(),
        NotamsChecked = f.NotamsChecked,
        AirspaceStructureNotes = f.AirspaceStructureNotes,
        AirspaceRestrictions = f.AirspaceRestrictions,
        Obstacles = f.Obstacles,
        FirstAidAndExtinguisher = f.FirstAidAndExtinguisher,
        PassengerListFilled = f.PassengerListFilled,
        FlightPlanFiled = f.FlightPlanFiled,
        BurnerTested = f.BurnerTested,
        GasCylindersChecked = f.GasCylindersChecked,
        BalloonVisualInspectionDone = f.BalloonVisualInspectionDone,
        MooringsChecked = f.MooringsChecked,
        InstrumentsChecked = f.InstrumentsChecked,
        EnvelopeWeightKg = f.EnvelopeWeightKg,
        BasketWeightKg = f.BasketWeightKg,
        BurnerWeightKg = f.BurnerWeightKg,
        CylindersWeightKg = f.CylindersWeightKg,
        MaxAltitudeFt = f.MaxAltitudeFt,
        LiftUnits = f.LiftUnits,
        TotalLiftKg = f.TotalLiftKg,
        LoadRemarks = f.LoadRemarks,
        Passengers = f.Passengers.OrderBy(p => p.Order).Select(p => new PassengerDto(p.Name, p.WeightKg, p.NeedsAssistance, p.IsChild, p.NeedsTransport)).ToList(),
        RouteDescription = f.RouteDescription,
        ClimbRateMetersPerSecond = f.ClimbRateMetersPerSecond,
        DescentRateMetersPerSecond = f.DescentRateMetersPerSecond,
        CruiseAltitudesFtCsv = f.CruiseAltitudesFtCsv,
        BulletinText = f.BulletinText,
        OfpOperatorName = f.OfpOperatorName,
        OfpPicWeightKg = f.OfpPicWeightKg,
        OfpFuelAvailableMinutes = f.OfpFuelAvailableMinutes,
        OfpFuelRequiredMinutes = f.OfpFuelRequiredMinutes,
        OfpLandingLocation = f.OfpLandingLocation,
        PaxBriefingText = f.PaxBriefingText,
        PaxBriefingVersion = f.PaxBriefingVersion,
        PaxBriefingIsCustom = f.PaxBriefingIsCustom,
    };

    private static string? Text(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
