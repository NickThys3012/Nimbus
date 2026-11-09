using FluentValidation;
namespace Nimbus.Application.Features.FlightAssessment.Queries.AssessFlight;

public sealed class AssessFlightQueryValidator : AbstractValidator<AssessFlightQuery>
{
    public AssessFlightQueryValidator()
    {
        RuleFor(q => q.Flight).NotNull();
        When(q => q.Flight is not null, () =>
        {
            RuleFor(q => q.Flight.EnvelopeWeightKg).InclusiveBetween(0m, 10000m);
            RuleFor(q => q.Flight.BasketWeightKg).InclusiveBetween(0m, 10000m);
            RuleFor(q => q.Flight.BurnerWeightKg).InclusiveBetween(0m, 10000m);
            RuleFor(q => q.Flight.CylindersWeightKg).InclusiveBetween(0m, 10000m);
            RuleFor(q => q.Flight.PilotWeightKg).InclusiveBetween(0m, 1000m);
            RuleFor(q => q.Flight.TotalLiftKg).InclusiveBetween(0m, 100000m);
            RuleFor(q => q.Flight.PassengerWeightsKg).NotNull().Must(w => w is null || w.Count <= 50).WithMessage("At most 50 passengers.");
            RuleForEach(q => q.Flight.PassengerWeightsKg).InclusiveBetween(0m, 1000m);
            RuleFor(q => q.Flight.WindSpeedKt).InclusiveBetween(0m, 500m).When(q => q.Flight.WindSpeedKt is not null);
            RuleFor(q => q.Flight.VisibilityKm).InclusiveBetween(0m, 1000m).When(q => q.Flight.VisibilityKm is not null);
            RuleFor(q => q.Flight.CapeJPerKg).InclusiveBetween(0m, 20000m).When(q => q.Flight.CapeJPerKg is not null);
        });
    }
}
