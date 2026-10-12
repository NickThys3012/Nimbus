using FluentValidation;
using Nimbus.Contracts.DTOs.Features.Pilots;
namespace Nimbus.Application.Features.Pilots;

/// <summary>Field rules shared by create and update.</summary>
public sealed class PilotRequestValidator : AbstractValidator<PilotRequestDto>
{
    public const decimal MinWeightKg = 20m;
    public const decimal MaxWeightKg = 250m;

    public PilotRequestValidator()
    {
        RuleFor(r => r.Name).NotEmpty().Must(n => n is null || n.Trim().Length > 0).WithMessage("'Name' must not be empty.");
        RuleFor(r => r.Name).MaximumLength(100);
        RuleFor(r => r.WeightKg).InclusiveBetween(MinWeightKg, MaxWeightKg);
        // The column is decimal(5,2): anything finer would be rounded by the database but echoed unrounded.
        RuleFor(r => r.WeightKg).Must(w => decimal.Round(w, 2) == w).WithMessage("'Weight Kg' must have at most 2 decimal places.");
    }
}
