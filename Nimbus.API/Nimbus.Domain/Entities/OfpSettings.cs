namespace Nimbus.Domain.Entities;

/// <summary>
///     A user's OFP (operational flight plan) settings (issue #28): one row per user. Resetting to
///     the default removes the row. Not soft-deleted.
/// </summary>
public class OfpSettings
{
    public Guid Id { get; set; }
    public Guid OwnerId { get; set; }

    /// <summary>Equipment weight counted per passenger in the OFP total weight only (never the basic weight check).</summary>
    public decimal EquipmentWeightPerPassengerKg { get; set; }

    public DateTime? UpdatedDate { get; set; }
}
