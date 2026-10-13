using Nimbus.Contracts.DTOs.Features.OfpSettings;
namespace Nimbus.Application.Features.OfpSettings;

/// <summary>Built-in default (issue #28) used when the user has no row.</summary>
public static class OfpSettingsDefaults
{
    public const decimal EquipmentWeightPerPassengerKg = 7m;

    public static OfpSettingsDto Effective() => new(EquipmentWeightPerPassengerKg, false);
}
