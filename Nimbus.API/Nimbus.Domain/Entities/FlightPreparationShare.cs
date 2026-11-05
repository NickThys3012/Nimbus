using Nimbus.Domain.Entities.Base;
namespace Nimbus.Domain.Entities;

/// <summary>
///     Grants another user read access to a flight preparation (issue #65/#66). References the
///     recipient by id only (same convention as <c>OwnerId</c>: no FK constraint to Identity).
///     Cascade-deletes with the parent <see cref="FlightPreparation" />; deleting a share row
///     revokes that user's access without affecting the dossier itself.
/// </summary>
public class FlightPreparationShare : BaseEntity
{
    public Guid FlightPreparationId { get; set; }
    public FlightPreparation FlightPreparation { get; set; } = null!;

    /// <summary>The id of the user the flight preparation is shared with.</summary>
    public Guid SharedWithUserId { get; set; }
}
