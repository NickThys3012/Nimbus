using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Nimbus.Application.Common.Exceptions;
using Nimbus.Domain.Entities;
using Nimbus.Domain.Interfaces;
namespace Nimbus.Infrastructure.Persistence.Repositories;

public class BalloonRepository : IBalloonRepository
{
    // SQL Server: 2601 = duplicate key in a unique index, 2627 = unique constraint violation.
    private static readonly int[] UniqueViolationCodes = [2601, 2627];

    private readonly AppDbContext _db;

    public BalloonRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<Balloon?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        // Not FindAsync: it bypasses the soft-delete query filter.
        return _db.Balloons.FirstOrDefaultAsync(b => b.Id == id, ct);
    }

    public async Task<IReadOnlyList<Balloon>> ListAsync(CancellationToken ct = default)
    {
        return await _db.Balloons.AsNoTracking().OrderBy(b => b.Registration).ToListAsync(ct);
    }

    public async Task AddAsync(Balloon entity, CancellationToken ct = default)
    {
        _db.Balloons.Add(entity);
        await SaveAsync(entity, ct);
    }

    public async Task UpdateAsync(Balloon entity, CancellationToken ct = default)
    {
        await SaveAsync(entity, ct);
    }

    /// <summary>Soft delete (see <see cref="AppDbContext" />): the row stays so referencing flights still resolve it.</summary>
    public async Task RemoveAsync(Balloon entity, CancellationToken ct = default)
    {
        _db.Balloons.Remove(entity);
        await _db.SaveChangesAsync(ct);
    }

    public Task<bool> RegistrationExistsAsync(Guid ownerId, string registration, Guid? excludingId, CancellationToken ct = default)
    {
        var normalized = Balloon.NormalizeRegistration(registration);
        return _db.Balloons.AnyAsync(
            b => b.OwnerId == ownerId && b.Registration == normalized && b.Id != excludingId, ct);
    }

    // The service pre-checks the registration, but two concurrent requests can both pass it;
    // the filtered unique index is the authority.
    private async Task SaveAsync(Balloon entity, CancellationToken ct)
    {
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException sql && UniqueViolationCodes.Contains(sql.Number))
        {
            throw new DuplicateException(nameof(Balloon.Registration), "You already have a balloon with this registration.");
        }
    }
}
