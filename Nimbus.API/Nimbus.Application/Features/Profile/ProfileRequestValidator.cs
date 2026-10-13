using FluentValidation;
using Nimbus.Contracts.DTOs.Features.Profile;
namespace Nimbus.Application.Features.Profile;

/// <summary>Both fields are optional; when given, the name is at most 200 characters and the weight 0 to 500 kg (exclusive of 0), max 2 decimals.</summary>
public sealed class ProfileRequestValidator : AbstractValidator<ProfileRequestDto>
{
    public const int MaxOperatorNameLength = 200;
    public const decimal MaxPicWeightKg = 500m;

    public ProfileRequestValidator()
    {
        RuleFor(r => r.OperatorName).MaximumLength(MaxOperatorNameLength);
        RuleFor(r => r.PicWeightKg!.Value)
            .GreaterThan(0m).WithMessage("Enter a weight above 0 kg.")
            .LessThanOrEqualTo(MaxPicWeightKg).WithMessage($"Enter a weight of at most {MaxPicWeightKg} kg.")
            .Must(v => decimal.Round(v, 2) == v).WithMessage("'{PropertyName}' must have at most 2 decimal places.")
            .OverridePropertyName(nameof(ProfileRequestDto.PicWeightKg))
            .When(r => r.PicWeightKg.HasValue);
    }
}
