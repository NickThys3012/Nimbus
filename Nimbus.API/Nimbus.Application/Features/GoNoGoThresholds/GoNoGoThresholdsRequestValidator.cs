using FluentValidation;
using Nimbus.Contracts.DTOs.Features.GoNoGoThresholds;
namespace Nimbus.Application.Features.GoNoGoThresholds;

/// <summary>Ranges plus consistency: no-go limits must be stricter than caution limits.</summary>
public sealed class GoNoGoThresholdsRequestValidator : AbstractValidator<GoNoGoThresholdsRequestDto>
{
    public const decimal MaxWindKt = 100m;
    public const decimal MaxVisibilityKm = 100m;
    public const decimal MaxCape = 10000m;

    public GoNoGoThresholdsRequestValidator()
    {
        Limit(r => r.WindYellowKt, MaxWindKt);
        Limit(r => r.WindRedKt, MaxWindKt);
        Limit(r => r.VisibilityYellowKm, MaxVisibilityKm);
        Limit(r => r.VisibilityRedKm, MaxVisibilityKm);
        Limit(r => r.CapeYellow, MaxCape);
        Limit(r => r.CapeRed, MaxCape);

        RuleFor(r => r.WindRedKt).GreaterThan(r => r.WindYellowKt)
            .WithMessage(r => $"Must be higher than {r.WindYellowKt} kt.");
        RuleFor(r => r.VisibilityRedKm).LessThan(r => r.VisibilityYellowKm)
            .WithMessage(r => $"Must be lower than {r.VisibilityYellowKm} km.");
        RuleFor(r => r.CapeRed).GreaterThan(r => r.CapeYellow)
            .WithMessage(r => $"Must be higher than {r.CapeYellow} J/kg.");
    }

    private void Limit(System.Linq.Expressions.Expression<Func<GoNoGoThresholdsRequestDto, decimal>> p, decimal max)
    {
        RuleFor(p).GreaterThan(0m).LessThanOrEqualTo(max);
        // The columns are decimal(7,2): finer input would be rounded by the database but echoed unrounded.
        RuleFor(p).Must(v => decimal.Round(v, 2) == v).WithMessage("'{PropertyName}' must have at most 2 decimal places.");
    }
}
