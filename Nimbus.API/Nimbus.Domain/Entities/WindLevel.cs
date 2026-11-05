using Nimbus.Domain.Entities.Base;
namespace Nimbus.Domain.Entities;

/// <summary>
///     A single altitude's wind reading in the section 02 (meteo) wind-on-altitude table (issue
///     #31). Cascade-deletes with the parent <see cref="FlightPreparation" />.
/// </summary>
public class WindLevel : BaseEntity
{
    public Guid FlightPreparationId { get; set; }
    public FlightPreparation FlightPreparation { get; set; } = null!;

    public int AltitudeM { get; set; }
    public int DirectionDeg { get; set; }
    public decimal SpeedKt { get; set; }
    public decimal TemperatureC { get; set; }

    /// <summary>Display order (lowest altitude first by convention); part of the <c>(FlightPreparationId, Order)</c> index.</summary>
    public int Order { get; set; }
}
