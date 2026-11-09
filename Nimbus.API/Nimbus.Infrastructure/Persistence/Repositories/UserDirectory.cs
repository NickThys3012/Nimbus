using Microsoft.EntityFrameworkCore;
using Nimbus.Domain.Interfaces;
namespace Nimbus.Infrastructure.Persistence.Repositories;

/// <summary>Batched display-name lookup: a single <c>WHERE Id IN (...)</c> query, selecting only the name columns.</summary>
public sealed class UserDirectory : IUserDirectory
{
    private readonly AppDbContext _db;

    public UserDirectory(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyDictionary<Guid, string>> GetDisplayNamesAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct = default)
    {
        if (userIds.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        var ids = userIds.Select(i => i.ToString()).Distinct().ToList();
        var rows = await _db.Users.AsNoTracking().Where(u => ids.Contains(u.Id))
            .Select(u => new { u.Id, u.FirstName, u.Name, u.Email })
            .ToListAsync(ct);
        return rows.ToDictionary(
            u => Guid.Parse(u.Id),
            u => string.IsNullOrWhiteSpace($"{u.FirstName} {u.Name}") ? u.Email ?? "" : $"{u.FirstName} {u.Name}".Trim());
    }
}
