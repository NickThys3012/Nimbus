using FluentValidation;
namespace Nimbus.Application.Features.GoNoGoThresholds.Queries.EvaluateGoNoGo;

public sealed class EvaluateGoNoGoQueryValidator : AbstractValidator<EvaluateGoNoGoQuery>
{
    public EvaluateGoNoGoQueryValidator()
    {
        RuleFor(q => q.WindSpeedKt).InclusiveBetween(0m, 500m).When(q => q.WindSpeedKt is not null);
        RuleFor(q => q.VisibilityKm).InclusiveBetween(0m, 1000m).When(q => q.VisibilityKm is not null);
        RuleFor(q => q.CapeJPerKg).InclusiveBetween(0m, 20000m).When(q => q.CapeJPerKg is not null);
    }
}
