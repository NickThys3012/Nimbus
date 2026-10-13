using Nimbus.Domain.Entities;
namespace Nimbus.Domain.Interfaces;

/// <summary>Every method takes the owner id; a profile is never addressed by its row id.</summary>
public interface IUserProfileRepository
{
    /// <summary>The tracked profile of exactly this user, or null.</summary>
    Task<UserProfile?> GetByOwnerAsync(Guid ownerId, CancellationToken ct = default);

    Task AddAsync(UserProfile entity, CancellationToken ct = default);
    Task UpdateAsync(UserProfile entity, CancellationToken ct = default);
}
