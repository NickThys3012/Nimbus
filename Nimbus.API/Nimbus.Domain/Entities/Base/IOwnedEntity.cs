namespace Nimbus.Domain.Entities.Base;

/// <summary>
///     Convention for every entity that belongs to a single user. The owner is always stamped
///     from the authenticated principal by the service layer — never taken from a request.
/// </summary>
public interface IOwnedEntity
{
    Guid Id { get; }
    Guid OwnerId { get; set; }
}
