using FluentValidation;
using Nimbus.Contracts.DTOs.Features.Balloons;
namespace Nimbus.Application.Features.Balloons;

/// <summary>Field rules shared by create and update.</summary>
public sealed class BalloonRequestValidator : AbstractValidator<BalloonRequestDto>
{
    public const decimal MaxWeightKg = 5000m;

    public BalloonRequestValidator()
    {
        RuleFor(r => r.Registration).NotEmpty().MaximumLength(20);
        RuleFor(r => r.Type).MaximumLength(100); // optional
        RuleFor(r => r.EnvelopeVolumeM3).InclusiveBetween(1, 20_000);
        RuleFor(r => r.MaxEnvelopeTemperatureC).InclusiveBetween(1, 250);
        WeightRule(r => r.EnvelopeWeightKg);
        WeightRule(r => r.BasketWeightKg);
        WeightRule(r => r.BurnerWeightKg);
        WeightRule(r => r.CylindersWeightKg);
    }

    // The column is decimal(7,2): anything finer would be rounded by the database but echoed unrounded.
    private void WeightRule(System.Linq.Expressions.Expression<Func<BalloonRequestDto, decimal>> weight)
    {
        RuleFor(weight).InclusiveBetween(0m, MaxWeightKg);
        RuleFor(weight).Must(w => decimal.Round(w, 2) == w).WithMessage("'{PropertyName}' must have at most 2 decimal places.");
    }
}
