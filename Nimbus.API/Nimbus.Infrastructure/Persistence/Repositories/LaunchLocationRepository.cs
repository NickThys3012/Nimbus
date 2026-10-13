using Microsoft.EntityFrameworkCore;
using Nimbus.Domain.Entities;
using Nimbus.Domain.Interfaces;
namespace Nimbus.Infrastructure.Persistence.Repositories;

public class LaunchLocationRepository : ILaunchLocationRepository
{
    private readonly AppDbContext _db;

    public LaunchLocationRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<LaunchLocation?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        // Not FindAsync: it bypasses the soft-delete query filter.
        return _db.LaunchLocations.FirstOrDefaultAsync(p => p.Id == id, ct);
    }

    public async Task<IReadOnlyList<LaunchLocation>> ListAsync(CancellationToken ct = default)
    {
        return await _db.LaunchLocations.AsNoTracking().OrderBy(p => p.Name).ThenBy(p => p.Id).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<LaunchLocation>> ListByOwnerAsync(Guid ownerId, CancellationToken ct = default)
    {
        return await _db.LaunchLocations.AsNoTracking().Where(p => p.OwnerId == ownerId).OrderBy(p => p.Name).ThenBy(p => p.Id).ToListAsync(ct);
    }

    public async Task AddAsync(LaunchLocation entity, CancellationToken ct = default)
    {
        _db.LaunchLocations.Add(entity);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(LaunchLocation entity, CancellationToken ct = default)
    {
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Soft delete (see <see cref="AppDbContext" />): the row stays so referencing flights still resolve it.</summary>
    public async Task RemoveAsync(LaunchLocation entity, CancellationToken ct = default)
    {
        _db.LaunchLocations.Remove(entity);
        await _db.SaveChangesAsync(ct);
    }
}
