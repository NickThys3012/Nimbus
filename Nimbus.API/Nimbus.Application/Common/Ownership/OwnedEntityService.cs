using Microsoft.Extensions.Logging;
using Nimbus.Domain.Entities.Base;
using Nimbus.Domain.Interfaces;
namespace Nimbus.Application.Common.Ownership;

public interface IOwnedEntityService<TEntity> where TEntity : class, IOwnedEntity
{
    Task<TEntity?> GetAsync(Caller caller, Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<TEntity>> ListAsync(Caller caller, CancellationToken ct = default);
    Task<TEntity> CreateAsync(Caller caller, TEntity entity, CancellationToken ct = default);
    Task<OwnedResult> UpdateAsync(Caller caller, Guid id, TEntity incoming, CancellationToken ct = default);
    Task<OwnedResult> DeleteAsync(Caller caller, Guid id, CancellationToken ct = default);
}

/// <summary>
///     Base for services over owned entities. Every path re-checks ownership here (admins may
///     access everything), so correctness never depends on the UI or controller having filtered.
///     Unauthorised operations log a warning and no-op instead of throwing.
/// </summary>
public abstract class OwnedEntityService<TEntity> : IOwnedEntityService<TEntity>
    where TEntity : class, IOwnedEntity
{
    private readonly ILogger _logger;
    private readonly IOwnedRepository<TEntity> _repository;

    protected OwnedEntityService(IOwnedRepository<TEntity> repository, ILogger logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<TEntity?> GetAsync(Caller caller, Guid id, CancellationToken ct = default)
    {
        var entity = await _repository.GetByIdAsync(id, ct);
        return entity is not null && CanAccess(caller, entity, "read") ? entity : null;
    }

    public async Task<IReadOnlyList<TEntity>> ListAsync(Caller caller, CancellationToken ct = default)
    {
        var all = await _repository.ListAsync(ct);
        return caller.IsAdmin ? all : all.Where(e => e.OwnerId == caller.UserId).ToList();
    }

    public async Task<TEntity> CreateAsync(Caller caller, TEntity entity, CancellationToken ct = default)
    {
        // Any owner supplied by the caller is ignored.
        entity.OwnerId = caller.UserId;
        await _repository.AddAsync(entity, ct);
        return entity;
    }

    public async Task<OwnedResult> UpdateAsync(
        Caller caller, Guid id, TEntity incoming, CancellationToken ct = default)
    {
        var existing = await _repository.GetByIdAsync(id, ct);
        if (existing is null || !CanAccess(caller, existing, "update"))
        {
            return OwnedResult.NotFound;
        }

        var ownerId = existing.OwnerId;
        CopyValues(existing, incoming);
        existing.OwnerId = ownerId; // a bulk copy must never move the entity to another owner

        await _repository.UpdateAsync(existing, ct);
        return OwnedResult.Success;
    }

    public async Task<OwnedResult> DeleteAsync(Caller caller, Guid id, CancellationToken ct = default)
    {
        var existing = await _repository.GetByIdAsync(id, ct);
        if (existing is null || !CanAccess(caller, existing, "delete"))
        {
            return OwnedResult.NotFound;
        }

        await _repository.RemoveAsync(existing, ct);
        return OwnedResult.Success;
    }

    /// <summary>Copies the editable values from <paramref name="source" /> onto <paramref name="target" />.</summary>
    protected abstract void CopyValues(TEntity target, TEntity source);

    private bool CanAccess(Caller caller, TEntity entity, string operation)
    {
        if (caller.IsAdmin || entity.OwnerId == caller.UserId)
        {
            return true;
        }

        _logger.LogWarning(
            "User {UserId} attempted to {Operation} {EntityType} {EntityId} owned by another user",
            caller.UserId, operation, typeof(TEntity).Name, entity.Id);
        return false;
    }
}
