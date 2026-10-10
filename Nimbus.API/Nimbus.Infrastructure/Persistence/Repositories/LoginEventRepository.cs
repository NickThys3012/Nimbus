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
        _db.LoginEvents.Add(loginEvent);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<LoginEvent>> ListRecentAsync(int limit, CancellationToken ct = default)
    {
        return await _db.LoginEvents.AsNoTracking()
            .OrderByDescending(e => e.OccurredAt)
            .Take(limit)
            .ToListAsync(ct);
    }
}
