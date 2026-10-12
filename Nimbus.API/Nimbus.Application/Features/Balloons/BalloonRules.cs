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
        RuleFor(r => r.EnvelopeWeightKg).InclusiveBetween(0m, MaxWeightKg);
        RuleFor(r => r.BasketWeightKg).InclusiveBetween(0m, MaxWeightKg);
        RuleFor(r => r.BurnerWeightKg).InclusiveBetween(0m, MaxWeightKg);
        RuleFor(r => r.CylindersWeightKg).InclusiveBetween(0m, MaxWeightKg);
    }
}
