using Nimbus.Contracts.DTOs.Features.OfpSettings;
using Nimbus.Domain.Entities;
namespace Nimbus.Contracts.Mappers;

public static class OfpSettingsMappers
{
    public static OfpSettingsDto MapToDto(this OfpSettings s, bool isCustom) => new(s.EquipmentWeightPerPassengerKg, isCustom);

    public static void ApplyTo(this OfpSettingsRequestDto r, OfpSettings target)
    {
        target.EquipmentWeightPerPassengerKg = r.EquipmentWeightPerPassengerKg;
    }
}
