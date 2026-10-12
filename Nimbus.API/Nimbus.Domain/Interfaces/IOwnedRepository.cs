using Nimbus.Domain.Entities.Base;
namespace Nimbus.Domain.Interfaces;

/// <summary>
///     Persistence for an owned entity. Deliberately owner-agnostic: authorisation lives in
///     <c>OwnedEntityService&lt;T&gt;</c>, so repositories never need to know who is calling.
/// </summary>
public interface IOwnedRepository<TEntity> where TEntity : class, IOwnedEntity
{
    Task<TEntity?> GetByIdAsync(Guid id, CancellationToken ct = default);
    /// <summary>Every owner's entities. Only for admins (see <c>OwnedEntityService&lt;T&gt;.ListAsync</c>).</summary>
    Task<IReadOnlyList<TEntity>> ListAsync(CancellationToken ct = default);

    /// <summary>The entities of one owner, filtered in the database.</summary>
    Task<IReadOnlyList<TEntity>> ListByOwnerAsync(Guid ownerId, CancellationToken ct = default);
    Task AddAsync(TEntity entity, CancellationToken ct = default);
    Task UpdateAsync(TEntity entity, CancellationToken ct = default);
    Task RemoveAsync(TEntity entity, CancellationToken ct = default);
}
