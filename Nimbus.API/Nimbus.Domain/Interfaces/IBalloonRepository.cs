using Nimbus.Domain.Entities;
namespace Nimbus.Domain.Interfaces;

public interface IBalloonRepository : IOwnedRepository<Balloon>
{
    /// <summary>True when <paramref name="ownerId" /> already has a (non-deleted) balloon with this registration.</summary>
    Task<bool> RegistrationExistsAsync(Guid ownerId, string registration, Guid? excludingId, CancellationToken ct = default);
}
