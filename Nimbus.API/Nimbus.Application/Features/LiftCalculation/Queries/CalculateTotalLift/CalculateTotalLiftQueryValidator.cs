using FluentValidation;
namespace Nimbus.Application.Features.LiftCalculation.Queries.CalculateTotalLift;

public sealed class CalculateTotalLiftQueryValidator : AbstractValidator<CalculateTotalLiftQuery>
{
    public CalculateTotalLiftQueryValidator()
    {
        RuleFor(q => q.MaxAltitudeMeters).InclusiveBetween(-500m, 12000m);
        RuleFor(q => q.SiteElevationMeters).InclusiveBetween(-500m, 9000m);
        RuleFor(q => q.AmbientTemperatureC).InclusiveBetween(-60m, 60m);
        // Values above 100 C are accepted and clamped by the calculator, as the manual caps the average at 100 C.
        RuleFor(q => q.InternalTemperatureC).InclusiveBetween(-60m, 300m);
        RuleFor(q => q.EnvelopeVolumeM3).InclusiveBetween(0m, 100000m);
    }
}
