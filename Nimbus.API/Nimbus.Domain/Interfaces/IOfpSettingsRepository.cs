using Nimbus.Domain.Entities;
namespace Nimbus.Domain.Interfaces;

/// <summary>Every method takes the owner id; there is no way to address a row by id alone.</summary>
public interface IOfpSettingsRepository
{
    /// <summary>The tracked row of exactly this user, or null.</summary>
    Task<OfpSettings?> GetByOwnerAsync(Guid ownerId, CancellationToken ct = default);

    Task AddAsync(OfpSettings entity, CancellationToken ct = default);
    Task UpdateAsync(OfpSettings entity, CancellationToken ct = default);
    Task RemoveAsync(OfpSettings entity, CancellationToken ct = default);
}
