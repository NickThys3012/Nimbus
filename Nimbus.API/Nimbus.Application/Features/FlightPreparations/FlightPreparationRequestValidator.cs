using FluentValidation;
using Nimbus.Contracts.DTOs.Features.FlightPreparations;
using Nimbus.Contracts.Mappers;
namespace Nimbus.Application.Features.FlightPreparations;

/// <summary>
///     Field rules shared by create and update. Ranges follow the column precisions so the database never rounds or
///     rejects what the API accepted.
/// </summary>
public sealed class FlightPreparationRequestValidator : AbstractValidator<FlightPreparationRequestDto>
{
    public const int MaxPassengers = 30;
    public const int MaxWindLevels = 30;

    public FlightPreparationRequestValidator()
    {
        RuleFor(r => r.Title).MaximumLength(FlightPreparationMappers.MaxTitleLength);
        RuleFor(r => r.BalloonId).NotEmpty();
        RuleFor(r => r.PilotId).NotEmpty();
        RuleFor(r => r.LaunchLocationId).NotEmpty();
        RuleFor(r => r.FlightDate).Must(d => d != default).WithMessage("'Flight Date' must be a real date.");

        RuleFor(r => r.WindSpeedKt).InclusiveBetween(0m, 9999m);
        RuleFor(r => r.WindDirectionDeg).InclusiveBetween(0, 360);
        RuleFor(r => r.VisibilityKm).InclusiveBetween(0m, 9999m);
        RuleFor(r => r.CapeJPerKg).InclusiveBetween(0m, 99999m);
        RuleFor(r => r.TemperatureC).InclusiveBetween(-99m, 99m);
        RuleFor(r => r.DewPointC).InclusiveBetween(-99m, 99m);
        RuleFor(r => r.QnhHpa).InclusiveBetween(800m, 1100m).When(r => r.QnhHpa is > 0m);
        RuleFor(r => r.Metar).MaximumLength(500);
        RuleFor(r => r.Taf).MaximumLength(1000);

        RuleFor(r => r.AirspaceStructureNotes).MaximumLength(2000);
        RuleFor(r => r.AirspaceRestrictions).MaximumLength(2000);
        RuleFor(r => r.Obstacles).MaximumLength(2000);

        RuleFor(r => r.FirstAidAndExtinguisher).IsInEnum();
        RuleFor(r => r.PassengerListFilled).IsInEnum();
        RuleFor(r => r.FlightPlanFiled).IsInEnum();

        RuleFor(r => r.EnvelopeWeightKg).InclusiveBetween(0m, 10000m);
        RuleFor(r => r.BasketWeightKg).InclusiveBetween(0m, 10000m);
        RuleFor(r => r.BurnerWeightKg).InclusiveBetween(0m, 10000m);
        RuleFor(r => r.CylindersWeightKg).InclusiveBetween(0m, 10000m);
        RuleFor(r => r.MaxAltitudeFt).InclusiveBetween(0, 40000);
        RuleFor(r => r.LiftUnits).InclusiveBetween(0m, 10000m);
        RuleFor(r => r.TotalLiftKg).InclusiveBetween(0m, 10000m);
        RuleFor(r => r.LoadRemarks).MaximumLength(2000);

        RuleFor(r => r.RouteDescription).MaximumLength(2000);
        RuleFor(r => r.ClimbRateMetersPerSecond).InclusiveBetween(0m, 99m);
        RuleFor(r => r.DescentRateMetersPerSecond).InclusiveBetween(0m, 99m);
        RuleFor(r => r.CruiseAltitudesFtCsv).MaximumLength(100);
        RuleFor(r => r.BulletinText).MaximumLength(4000);

        RuleFor(r => r.OfpOperatorName).MaximumLength(200);
        RuleFor(r => r.OfpPicWeightKg).InclusiveBetween(0m, 999m);
        RuleFor(r => r.OfpFuelAvailableMinutes).InclusiveBetween(0, 10000);
        RuleFor(r => r.OfpFuelRequiredMinutes).InclusiveBetween(0, 10000);
        RuleFor(r => r.OfpLandingLocation).MaximumLength(200);

        RuleFor(r => r.PaxBriefingVersion).MaximumLength(50);

        RuleFor(r => r.WindLevels).NotNull().Must(l => l.Count <= MaxWindLevels).WithMessage($"At most {MaxWindLevels} wind levels.");
        RuleForEach(r => r.WindLevels).ChildRules(w =>
        {
            w.RuleFor(x => x.AltitudeM).InclusiveBetween(0, 20000);
            w.RuleFor(x => x.DirectionDeg).InclusiveBetween(0, 360);
            w.RuleFor(x => x.SpeedKt).InclusiveBetween(0m, 999m);
            w.RuleFor(x => x.TemperatureC).InclusiveBetween(-99m, 99m);
        });

        RuleFor(r => r.Passengers).NotNull().Must(l => l.Count <= MaxPassengers).WithMessage($"At most {MaxPassengers} passengers.");
        RuleForEach(r => r.Passengers).ChildRules(p =>
        {
            p.RuleFor(x => x.Name).NotEmpty().Must(n => n is null || n.Trim().Length > 0).WithMessage("'Name' must not be empty.");
            p.RuleFor(x => x.Name).MaximumLength(200);
            p.RuleFor(x => x.WeightKg).InclusiveBetween(1m, 500m);
        });
    }
}
