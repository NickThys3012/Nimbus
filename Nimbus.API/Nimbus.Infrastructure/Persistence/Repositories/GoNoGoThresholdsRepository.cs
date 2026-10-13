using Microsoft.EntityFrameworkCore;
using Nimbus.Domain.Entities;
using Nimbus.Domain.Interfaces;
namespace Nimbus.Infrastructure.Persistence.Repositories;

public class GoNoGoThresholdsRepository : IGoNoGoThresholdsRepository
{
    private readonly AppDbContext _db;

    public GoNoGoThresholdsRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<GoNoGoThreshold?> GetByOwnerAsync(Guid ownerId, CancellationToken ct = default)
    {
        return _db.GoNoGoThresholds.FirstOrDefaultAsync(t => t.OwnerId == ownerId, ct);
    }

    public Task<GoNoGoThreshold?> GetGlobalDefaultAsync(CancellationToken ct = default)
    {
        return _db.GoNoGoThresholds.AsNoTracking().FirstOrDefaultAsync(t => t.OwnerId == null, ct);
    }

    public async Task AddAsync(GoNoGoThreshold entity, CancellationToken ct = default)
    {
        entity.UpdatedDate = DateTime.UtcNow;
        _db.GoNoGoThresholds.Add(entity);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(GoNoGoThreshold entity, CancellationToken ct = default)
    {
        entity.UpdatedDate = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task RemoveAsync(GoNoGoThreshold entity, CancellationToken ct = default)
    {
        _db.GoNoGoThresholds.Remove(entity);
        await _db.SaveChangesAsync(ct);
    }
}
