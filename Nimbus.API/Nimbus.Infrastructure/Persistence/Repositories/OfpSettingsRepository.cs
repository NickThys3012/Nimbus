using Microsoft.EntityFrameworkCore;
using Nimbus.Domain.Entities;
using Nimbus.Domain.Interfaces;
namespace Nimbus.Infrastructure.Persistence.Repositories;

public class OfpSettingsRepository : IOfpSettingsRepository
{
    private readonly AppDbContext _db;

    public OfpSettingsRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<OfpSettings?> GetByOwnerAsync(Guid ownerId, CancellationToken ct = default)
    {
        return _db.OfpSettings.FirstOrDefaultAsync(s => s.OwnerId == ownerId, ct);
    }

    public async Task AddAsync(OfpSettings entity, CancellationToken ct = default)
    {
        entity.UpdatedDate = DateTime.UtcNow;
        _db.OfpSettings.Add(entity);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(OfpSettings entity, CancellationToken ct = default)
    {
        entity.UpdatedDate = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task RemoveAsync(OfpSettings entity, CancellationToken ct = default)
    {
        _db.OfpSettings.Remove(entity);
        await _db.SaveChangesAsync(ct);
    }
}
