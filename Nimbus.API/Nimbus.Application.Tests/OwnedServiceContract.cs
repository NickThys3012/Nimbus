using Nimbus.Application.Common.Ownership;
using Nimbus.Domain.Entities.Base;
using Nimbus.Domain.Interfaces;
namespace Nimbus.Application.Tests;

/// <summary>
///     Reusable assertions for the owner-scoping rules (issue #23). Covering a new owned entity:
///     <code>
///     await OwnedServiceContract.AssertHoldsAsync(
///         repo => new FooService(repo, NullLogger.Instance),
///         () => new Foo { Name = "a" },
///         foo => foo.Name = "changed");
///     </code>
/// </summary>
public static class OwnedServiceContract
{
    public static async Task AssertHoldsAsync<TEntity>(
        Func<IOwnedRepository<TEntity>, IOwnedEntityService<TEntity>> createService,
        Func<TEntity> newEntity,
        Action<TEntity> mutate)
        where TEntity : class, IOwnedEntity
    {
        var owner = new Caller(Guid.NewGuid(), false);
        var stranger = new Caller(Guid.NewGuid(), false);
        var admin = new Caller(Guid.NewGuid(), true);

        var repo = new InMemoryOwnedRepository<TEntity>();
        var service = createService(repo);

        // Create stamps the owner from the caller and ignores a supplied one.
        var supplied = newEntity();
        supplied.OwnerId = stranger.UserId;
        var created = await service.CreateAsync(owner, supplied);
        Assert.That(created.OwnerId, Is.EqualTo(owner.UserId), "create must stamp the caller as owner");

        // Reads: owner and admin yes, stranger no.
        Assert.That(await service.GetAsync(owner, created.Id), Is.Not.Null);
        Assert.That(await service.GetAsync(admin, created.Id), Is.Not.Null);
        Assert.That(await service.GetAsync(stranger, created.Id), Is.Null, "stranger must not read");
        Assert.That(await service.ListAsync(stranger), Is.Empty, "list must be scoped to the caller");
        Assert.That(await service.ListAsync(owner), Has.Count.EqualTo(1));
        Assert.That(await service.ListAsync(admin), Has.Count.EqualTo(1));

        // Unauthorised update/delete no-op without throwing.
        var foreignEdit = newEntity();
        mutate(foreignEdit);
        Assert.That(await service.UpdateAsync(stranger, created.Id, foreignEdit), Is.EqualTo(OwnedResult.NotFound));
        Assert.That(repo.UpdateCount, Is.EqualTo(0), "stranger update must not persist");
        Assert.That(await service.DeleteAsync(stranger, created.Id), Is.EqualTo(OwnedResult.NotFound));
        Assert.That(repo.Count, Is.EqualTo(1), "stranger delete must not remove");

        // Update preserves the owner even when the incoming model carries another one.
        var incoming = newEntity();
        mutate(incoming);
        incoming.OwnerId = stranger.UserId;
        Assert.That(await service.UpdateAsync(owner, created.Id, incoming), Is.EqualTo(OwnedResult.Success));
        Assert.That(created.OwnerId, Is.EqualTo(owner.UserId), "update must preserve the owner");

        // Admin may modify, and still does not take ownership.
        Assert.That(await service.UpdateAsync(admin, created.Id, incoming), Is.EqualTo(OwnedResult.Success));
        Assert.That(created.OwnerId, Is.EqualTo(owner.UserId));

        // Missing ids are a no-op too.
        Assert.That(await service.DeleteAsync(owner, Guid.NewGuid()), Is.EqualTo(OwnedResult.NotFound));

        // Owner can delete.
        Assert.That(await service.DeleteAsync(owner, created.Id), Is.EqualTo(OwnedResult.Success));
        Assert.That(repo.Count, Is.EqualTo(0));
    }

    private sealed class InMemoryOwnedRepository<TEntity> : IOwnedRepository<TEntity>
        where TEntity : class, IOwnedEntity
    {
        private readonly List<TEntity> _items = [];
        public int UpdateCount { get; private set; }
        public int Count => _items.Count;

        public Task<TEntity?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(_items.FirstOrDefault(e => e.Id == id));

        public Task<IReadOnlyList<TEntity>> ListAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<TEntity>>(_items.ToList());

        public Task AddAsync(TEntity entity, CancellationToken ct = default)
        {
            _items.Add(entity);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(TEntity entity, CancellationToken ct = default)
        {
            UpdateCount++;
            return Task.CompletedTask;
        }

        public Task RemoveAsync(TEntity entity, CancellationToken ct = default)
        {
            _items.Remove(entity);
            return Task.CompletedTask;
        }
    }
}
