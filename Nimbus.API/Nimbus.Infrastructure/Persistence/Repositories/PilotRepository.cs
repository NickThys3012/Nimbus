using Microsoft.EntityFrameworkCore;
using Nimbus.Domain.Entities;
using Nimbus.Domain.Interfaces;
namespace Nimbus.Infrastructure.Persistence.Repositories;

public class PilotRepository : IPilotRepository
{
    private readonly AppDbContext _db;

    public PilotRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<Pilot?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        // Not FindAsync: it bypasses the soft-delete query filter.
        return _db.Pilots.FirstOrDefaultAsync(p => p.Id == id, ct);
    }

    public async Task<IReadOnlyList<Pilot>> ListAsync(CancellationToken ct = default)
    {
        return await _db.Pilots.AsNoTracking().OrderBy(p => p.Name).ThenBy(p => p.Id).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Pilot>> ListByOwnerAsync(Guid ownerId, CancellationToken ct = default)
    {
        return await _db.Pilots.AsNoTracking().Where(p => p.OwnerId == ownerId).OrderBy(p => p.Name).ThenBy(p => p.Id).ToListAsync(ct);
    }

    public async Task AddAsync(Pilot entity, CancellationToken ct = default)
    {
        _db.Pilots.Add(entity);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Pilot entity, CancellationToken ct = default)
    {
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Soft delete (see <see cref="AppDbContext" />): the row stays so referencing flights still resolve it.</summary>
    public async Task RemoveAsync(Pilot entity, CancellationToken ct = default)
    {
        _db.Pilots.Remove(entity);
        await _db.SaveChangesAsync(ct);
    }
}
