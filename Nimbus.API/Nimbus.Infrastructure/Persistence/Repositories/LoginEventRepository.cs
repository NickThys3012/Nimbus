using Microsoft.EntityFrameworkCore;
using Nimbus.Domain.Entities;
using Nimbus.Domain.Interfaces;
namespace Nimbus.Infrastructure.Persistence.Repositories;

public class LoginEventRepository : ILoginEventRepository
{
    private readonly AppDbContext _db;

    public LoginEventRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task AddAsync(LoginEvent loginEvent, CancellationToken ct = default)
    {
        var entry = _db.LoginEvents.Add(loginEvent);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch
        {
            // The context is shared with the rest of the request: never leave a failed audit row
            // tracked, or a later SaveChanges (e.g. token issuance) would retry it and fail too.
            entry.State = EntityState.Detached;
            throw;
        }
    }

    public async Task<IReadOnlyList<LoginEvent>> ListRecentAsync(int limit, CancellationToken ct = default)
    {
        return await _db.LoginEvents.AsNoTracking()
            .OrderByDescending(e => e.OccurredAt)
            .Take(limit)
            .ToListAsync(ct);
    }
}
