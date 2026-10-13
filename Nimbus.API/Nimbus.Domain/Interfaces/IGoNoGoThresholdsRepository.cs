using Nimbus.Domain.Entities;
namespace Nimbus.Domain.Interfaces;

/// <summary>Every user-facing method takes the owner id; the global default is reachable only read-only.</summary>
public interface IGoNoGoThresholdsRepository
{
    /// <summary>The tracked row of exactly this user, or null.</summary>
    Task<GoNoGoThreshold?> GetByOwnerAsync(Guid ownerId, CancellationToken ct = default);

    /// <summary>The (untracked, read-only) global default row (null owner), or null when none exists.</summary>
    Task<GoNoGoThreshold?> GetGlobalDefaultAsync(CancellationToken ct = default);

    Task AddAsync(GoNoGoThreshold entity, CancellationToken ct = default);
    Task UpdateAsync(GoNoGoThreshold entity, CancellationToken ct = default);
    Task RemoveAsync(GoNoGoThreshold entity, CancellationToken ct = default);
}
