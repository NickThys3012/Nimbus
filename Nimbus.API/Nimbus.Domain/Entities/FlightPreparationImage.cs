using Nimbus.Domain.Entities.Base;
using Nimbus.Domain.Enums;
namespace Nimbus.Domain.Entities;

/// <summary>
///     An image attached to one section of a flight preparation (issue #31). Stores an
///     object-storage reference only — never file bytes (see docs/object-storage.md) — tagged
///     with the section it belongs to and a display order. Cascade-deletes with the parent
///     <see cref="FlightPreparation" />; uploading via <see cref="Interfaces.IObjectStorageService" />
///     and cleaning up the backing object on delete are handled by the feature that uploads images
///     (issue #51), not here.
/// </summary>
public class FlightPreparationImage : BaseEntity
{
    public Guid FlightPreparationId { get; set; }
    public FlightPreparation FlightPreparation { get; set; } = null!;

    public FlightPreparationSection Section { get; set; }

    /// <summary>Object-storage key (<c>Nimbus.Domain.ValueObjects.ObjectKey</c>), not file content.</summary>
    public string ObjectKey { get; set; } = "";

    public string ContentType { get; set; } = "";

    /// <summary>Display order within its section; part of the <c>(FlightPreparationId, Section, Order)</c> index.</summary>
    public int Order { get; set; }
}
