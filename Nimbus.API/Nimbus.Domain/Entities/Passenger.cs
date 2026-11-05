using Nimbus.Domain.Entities.Base;
namespace Nimbus.Domain.Entities;

/// <summary>
///     A passenger on a flight preparation's load calculation (section 05, issue #31). Cascade-
///     deletes with the parent <see cref="FlightPreparation" />.
/// </summary>
public class Passenger : BaseEntity
{
    public Guid FlightPreparationId { get; set; }
    public FlightPreparation FlightPreparation { get; set; } = null!;

    public string Name { get; set; } = "";
    public decimal WeightKg { get; set; }
    public bool NeedsAssistance { get; set; }
    public bool IsChild { get; set; }

    /// <summary>Display order within the flight preparation; part of the <c>(FlightPreparationId, Order)</c> index.</summary>
    public int Order { get; set; }
}
