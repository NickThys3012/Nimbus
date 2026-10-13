using FluentValidation;
using Nimbus.Contracts.DTOs.Features.OfpSettings;
namespace Nimbus.Application.Features.OfpSettings;

public sealed class OfpSettingsRequestValidator : AbstractValidator<OfpSettingsRequestDto>
{
    public const decimal MaxKg = 100m;

    public OfpSettingsRequestValidator()
    {
        RuleFor(r => r.EquipmentWeightPerPassengerKg).InclusiveBetween(0m, MaxKg);
        // The column is decimal(5,2): finer input would be rounded by the database but echoed unrounded.
        RuleFor(r => r.EquipmentWeightPerPassengerKg).Must(v => decimal.Round(v, 2) == v)
            .WithMessage("'{PropertyName}' must have at most 2 decimal places.");
    }
}
