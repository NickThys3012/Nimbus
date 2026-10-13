using Microsoft.EntityFrameworkCore;
using Nimbus.Domain.Entities;
using Nimbus.Domain.Interfaces;
namespace Nimbus.Infrastructure.Persistence.Repositories;

public class UserProfileRepository : IUserProfileRepository
{
    private readonly AppDbContext _db;

    public UserProfileRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<UserProfile?> GetByOwnerAsync(Guid ownerId, CancellationToken ct = default)
    {
        return _db.UserProfiles.FirstOrDefaultAsync(p => p.OwnerId == ownerId, ct);
    }

    public async Task AddAsync(UserProfile entity, CancellationToken ct = default)
    {
        entity.UpdatedDate = DateTime.UtcNow;
        _db.UserProfiles.Add(entity);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(UserProfile entity, CancellationToken ct = default)
    {
        entity.UpdatedDate = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }
}
