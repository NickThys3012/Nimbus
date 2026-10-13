namespace Nimbus.Application.Features.OfpSettings;

/// <summary>
///     The OFP total-weight formula. This is the ONLY place the per-passenger equipment weight is used;
///     the basic weight check must never call it. The equipment weight is a parameter, so a flight
///     snapshots the value in effect when it is prepared and keeps that recorded figure afterwards:
///     changing the setting later never re-computes an already-flown flight.
/// </summary>
public static class OfpWeightCalculator
{
    /// <summary>Equipment weight counted in the OFP total: passengers x per-passenger equipment weight.</summary>
    public static decimal EquipmentWeightKg(int passengers, decimal equipmentWeightPerPassengerKg) =>
        passengers * equipmentWeightPerPassengerKg;

    /// <summary>OFP total = the basic weight (computed separately) plus the equipment weight.</summary>
    public static decimal TotalWeightKg(decimal basicWeightKg, int passengers, decimal equipmentWeightPerPassengerKg) =>
        basicWeightKg + EquipmentWeightKg(passengers, equipmentWeightPerPassengerKg);
}
