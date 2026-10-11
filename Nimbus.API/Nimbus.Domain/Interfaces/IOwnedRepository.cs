using Nimbus.Domain.Entities.Base;
namespace Nimbus.Domain.Interfaces;

/// <summary>
///     Persistence for an owned entity. Deliberately owner-agnostic: authorisation lives in
///     <c>OwnedEntityService&lt;T&gt;</c>, so repositories never need to know who is calling.
/// </summary>
public interface IOwnedRepository<TEntity> where TEntity : class, IOwnedEntity
{
    Task<TEntity?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<TEntity>> ListAsync(CancellationToken ct = default);
    Task AddAsync(TEntity entity, CancellationToken ct = default);
    Task UpdateAsync(TEntity entity, CancellationToken ct = default);
    Task RemoveAsync(TEntity entity, CancellationToken ct = default);
}
