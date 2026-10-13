namespace Nimbus.Contracts.DTOs.Features.OfpSettings;

/// <summary>Editable OFP settings. The owner is never part of a request.</summary>
public record OfpSettingsRequestDto(decimal EquipmentWeightPerPassengerKg);

/// <summary>The effective OFP settings of the caller. <paramref name="IsCustom" /> is false when the default applies.</summary>
public record OfpSettingsDto(decimal EquipmentWeightPerPassengerKg, bool IsCustom);
