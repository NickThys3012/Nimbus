using Microsoft.EntityFrameworkCore;
using Nimbus.Domain.Entities;
using Nimbus.Domain.Entities.Base;
using Nimbus.Domain.Interfaces;
namespace Nimbus.Infrastructure.Persistence.Repositories;

/// <summary>
///     Flight-preparation persistence (issue #42). Save design, in short:
///     <list type="bullet">
///         <item>Create/update run in an explicit transaction; any failure rolls back.</item>
///         <item>
///             The caller's navigation properties and child collections are detached before EF sees the
///             entity and restored in <c>finally</c>; on failure generated ids and audit dates are rolled
///             back too, so the in-memory model is never corrupted.
///         </item>
///         <item>Children are replaced (delete all, insert supplied), never diffed — see docs/flight-preparation.md.</item>
///     </list>
/// </summary>
public class FlightPreparationRepository : IFlightPreparationRepository
{
    private readonly AppDbContext _db;

    public FlightPreparationRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<FlightPreparation?> GetByIdAsync(Guid ownerId, Guid id, CancellationToken ct = default)
    {
        // Read-only: callers get a detached copy to edit and hand back to UpdateAsync, never a tracked graph.
        return _db.FlightPreparations
            .AsNoTracking()
            .Include(f => f.Passengers.OrderBy(p => p.Order))
            .Include(f => f.WindLevels.OrderBy(w => w.Order))
            .FirstOrDefaultAsync(f => f.Id == id && f.OwnerId == ownerId, ct);
    }

    public async Task AddAsync(FlightPreparation flight, CancellationToken ct = default)
    {
        if (flight.OwnerId == Guid.Empty)
            throw new ArgumentException("The flight must have an owner.", nameof(flight));

        var scope = new CallerGraph(flight);
        scope.Detach();
        var entries = new List<object>();

        // The retrying execution strategy must own the whole transaction so a retry replays
        // begin/commit together; a manually-scoped transaction isn't supported alongside it.
        // scope.Detach/Restore wrap every retry attempt exactly once: Restore(success) only
        // fires after the strategy either finishes successfully or gives up for good.
        var strategy = _db.Database.CreateExecutionStrategy();
        try
        {
            await strategy.ExecuteAsync(async () =>
            {
                entries.Clear();
                await using var tx = await _db.Database.BeginTransactionAsync(ct);
                try
                {
                    // 1. The flight alone: no navigation to children, so EF cannot cascade-insert anything.
                    _db.FlightPreparations.Add(flight);
                    entries.Add(flight);
                    await _db.SaveChangesAsync(ct);

                    // 2. Children re-parented to the generated id, order = position in the supplied list.
                    InsertChildren(flight.Id, scope.Passengers, scope.WindLevels, entries);
                    await _db.SaveChangesAsync(ct);

                    await tx.CommitAsync(ct);
                }
                catch
                {
                    await tx.RollbackAsync(CancellationToken.None);
                    DetachFromTracker(entries);
                    throw;
                }
            });
            scope.Restore(success: true);
        }
        catch
        {
            scope.Restore(success: false);
            throw;
        }
    }

    public async Task<bool> UpdateAsync(Guid ownerId, FlightPreparation flight, CancellationToken ct = default)
    {
        var scope = new CallerGraph(flight);
        scope.Detach();
        var entries = new List<object>();

        // See AddAsync: the retrying execution strategy must own the whole transaction, and
        // scope.Detach/Restore wrap every retry attempt exactly once.
        var strategy = _db.Database.CreateExecutionStrategy();
        try
        {
            var found = await strategy.ExecuteAsync(async () =>
            {
                entries.Clear();
                await using var tx = await _db.Database.BeginTransactionAsync(ct);
                try
                {
                    // Owner AND id: another user's flight looks exactly like a missing one.
                    var existing = await _db.FlightPreparations.FirstOrDefaultAsync(f => f.Id == flight.Id && f.OwnerId == ownerId, ct);
                    if (existing is null)
                    {
                        await tx.RollbackAsync(ct);
                        return false;
                    }

                    entries.Add(existing);
                    CopyDossierColumns(flight, existing);
                    await _db.SaveChangesAsync(ct);

                    // Replace strategy: hard-delete every existing child row (including soft-deleted ones,
                    // hence IgnoreQueryFilters), then insert the supplied list in order.
                    await _db.Passengers.IgnoreQueryFilters().Where(p => p.FlightPreparationId == existing.Id).ExecuteDeleteAsync(ct);
                    await _db.WindLevels.IgnoreQueryFilters().Where(w => w.FlightPreparationId == existing.Id).ExecuteDeleteAsync(ct);
                    InsertChildren(existing.Id, scope.Passengers, scope.WindLevels, entries);
                    await _db.SaveChangesAsync(ct);

                    await tx.CommitAsync(ct);
                    flight.UpdatedDate = existing.UpdatedDate;
                    return true;
                }
                catch
                {
                    await tx.RollbackAsync(CancellationToken.None);
                    DetachFromTracker(entries);
                    throw;
                }
            });
            scope.Restore(success: true);
            return found;
        }
        catch
        {
            scope.Restore(success: false);
            throw;
        }
    }

    public async Task<FlightDeletion?> DeleteAsync(Guid ownerId, Guid id, CancellationToken ct = default)
    {
        // One transaction: read the file references, then delete the row. The foreign keys cascade to passengers, wind levels,
        // images and shares, so nothing is left behind, and soft-deleted children go with it (hence IgnoreQueryFilters).
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        var kml = await _db.FlightPreparations.IgnoreQueryFilters().AsNoTracking()
            .Where(f => f.Id == id && f.OwnerId == ownerId)
            .Select(f => new { f.KmlTrackObjectKey })
            .FirstOrDefaultAsync(ct);
        if (kml is null)
        {
            await tx.RollbackAsync(ct);
            return null;
        }

        var images = await _db.FlightPreparationImages.IgnoreQueryFilters().AsNoTracking()
            .Where(i => i.FlightPreparationId == id)
            .OrderBy(i => i.Order)
            .Select(i => i.ObjectKey)
            .ToListAsync(ct);
        var rows = await _db.FlightPreparations.IgnoreQueryFilters()
            .Where(f => f.Id == id && f.OwnerId == ownerId)
            .ExecuteDeleteAsync(ct);
        if (rows == 0)
        {
            await tx.RollbackAsync(ct);
            return null;
        }

        await tx.CommitAsync(ct);
        return new FlightDeletion(images, string.IsNullOrWhiteSpace(kml.KmlTrackObjectKey) ? null : kml.KmlTrackObjectKey);
    }

    public async Task<bool> SetTrajectoryJsonAsync(Guid ownerId, Guid id, string? trajectoryJson, CancellationToken ct = default)
    {
        var rows = await _db.FlightPreparations
            .Where(f => f.Id == id && f.OwnerId == ownerId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(f => f.TrajectoryJson, trajectoryJson)
                .SetProperty(f => f.UpdatedDate, DateTime.UtcNow), ct);
        return rows > 0;
    }

    public async Task<bool> SetKmlTrackAsync(Guid ownerId, Guid id, string? objectKey, string? contentType, CancellationToken ct = default)
    {
        var rows = await _db.FlightPreparations
            .Where(f => f.Id == id && f.OwnerId == ownerId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(f => f.KmlTrackObjectKey, objectKey)
                .SetProperty(f => f.KmlTrackContentType, contentType)
                .SetProperty(f => f.UpdatedDate, DateTime.UtcNow), ct);
        return rows > 0;
    }

    private void InsertChildren(Guid flightId, List<Passenger> passengers, List<WindLevel> windLevels, List<object> entries)
    {
        for (var i = 0; i < passengers.Count; i++)
        {
            var p = passengers[i];
            p.FlightPreparationId = flightId;
            p.Order = i;
            _db.Passengers.Add(p);
            entries.Add(p);
        }

        for (var i = 0; i < windLevels.Count; i++)
        {
            var w = windLevels[i];
            w.FlightPreparationId = flightId;
            w.Order = i;
            _db.WindLevels.Add(w);
            entries.Add(w);
        }
    }

    // Everything the dossier form owns. Identity, ownership, audit/soft-delete flags and the
    // columns with their own patch operations are deliberately preserved from the stored row.
    private void CopyDossierColumns(FlightPreparation source, FlightPreparation target)
    {
        var (id, owner, created, deleted) = (target.Id, target.OwnerId, target.CreatedDate, target.IsDeleted);
        var (kmlKey, kmlType, trajectory) = (target.KmlTrackObjectKey, target.KmlTrackContentType, target.TrajectoryJson);

        _db.Entry(target).CurrentValues.SetValues(source);

        target.Id = id;
        target.OwnerId = owner;
        target.CreatedDate = created;
        target.IsDeleted = deleted;
        target.KmlTrackObjectKey = kmlKey;
        target.KmlTrackContentType = kmlType;
        target.TrajectoryJson = trajectory;
    }

    private void DetachFromTracker(IEnumerable<object> entities)
    {
        foreach (var e in entities)
            _db.Entry(e).State = EntityState.Detached;
    }

    /// <summary>
    ///     Snapshot of the caller's graph. <see cref="Detach" /> empties the collections and nulls the
    ///     children's parent navigation so EF only ever sees what the repository adds explicitly;
    ///     <see cref="Restore" /> (always called from the failure and success paths alike) puts them
    ///     back and, on failure, also undoes ids/audit dates EF generated.
    /// </summary>
    private sealed class CallerGraph
    {
        private readonly FlightPreparation _flight;
        private readonly ICollection<Passenger> _passengersCollection;
        private readonly ICollection<FlightPreparationImage> _imagesCollection;
        private readonly ICollection<WindLevel> _windLevelsCollection;
        private readonly ICollection<FlightPreparationShare> _sharesCollection;
        private readonly List<Action> _undo = [];

        public CallerGraph(FlightPreparation flight)
        {
            _flight = flight;
            _passengersCollection = flight.Passengers;
            _imagesCollection = flight.Images;
            _windLevelsCollection = flight.WindLevels;
            _sharesCollection = flight.Shares;
            Passengers = _passengersCollection.ToList();
            WindLevels = _windLevelsCollection.ToList();
        }

        public List<Passenger> Passengers { get; }
        public List<WindLevel> WindLevels { get; }

        public void Detach()
        {
            Snapshot(_flight);
            foreach (var p in Passengers)
            {
                Snapshot(p);
                var (fk, parent, order) = (p.FlightPreparationId, p.FlightPreparation, p.Order);
                _undo.Add(() => (p.FlightPreparationId, p.Order) = (fk, order));
                _parents.Add(() => p.FlightPreparation = parent);
                p.FlightPreparation = null!;
            }

            foreach (var w in WindLevels)
            {
                Snapshot(w);
                var (fk, parent, order) = (w.FlightPreparationId, w.FlightPreparation, w.Order);
                _undo.Add(() => (w.FlightPreparationId, w.Order) = (fk, order));
                _parents.Add(() => w.FlightPreparation = parent);
                w.FlightPreparation = null!;
            }

            _flight.Passengers = [];
            _flight.WindLevels = [];
            _flight.Images = [];
            _flight.Shares = [];
        }

        private readonly List<Action> _parents = [];

        public void Restore(bool success)
        {
            _flight.Passengers = _passengersCollection;
            _flight.WindLevels = _windLevelsCollection;
            _flight.Images = _imagesCollection;
            _flight.Shares = _sharesCollection;
            foreach (var restoreParent in _parents)
                restoreParent();
            if (!success)
                foreach (var undo in _undo)
                    undo();
        }

        private void Snapshot(BaseEntity e)
        {
            var (id, created, updated) = (e.Id, e.CreatedDate, e.UpdatedDate);
            _undo.Add(() => (e.Id, e.CreatedDate, e.UpdatedDate) = (id, created, updated));
        }
    }
}
