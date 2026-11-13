using Microsoft.EntityFrameworkCore;
using Nimbus.Domain.Entities;
using Nimbus.Domain.Interfaces;
namespace Nimbus.Infrastructure.Persistence.Repositories;

public sealed class FlightImageRepository(AppDbContext db) : IFlightImageRepository
{
    public async Task AddAsync(FlightPreparationImage image, CancellationToken ct = default)
    {
        var highest = await db.FlightPreparationImages
            .Where(i => i.FlightPreparationId == image.FlightPreparationId && i.Section == image.Section)
            .MaxAsync(i => (int?)i.Order, ct);
        image.Order = (highest ?? -1) + 1;

        db.FlightPreparationImages.Add(image);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            db.Entry(image).State = EntityState.Detached;
            throw;
        }
    }

    public Task<FlightPreparationImage?> GetAsync(Guid flightId, Guid imageId, CancellationToken ct = default)
    {
        return db.FlightPreparationImages.AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == imageId && i.FlightPreparationId == flightId, ct);
    }

    public async Task<IReadOnlyList<FlightPreparationImage>> ListAsync(Guid flightId, CancellationToken ct = default)
    {
        return await db.FlightPreparationImages.AsNoTracking()
            .Where(i => i.FlightPreparationId == flightId)
            .OrderBy(i => i.Section).ThenBy(i => i.Order).ThenBy(i => i.CreatedDate)
            .ToListAsync(ct);
    }

    public async Task SetOrderAsync(Guid flightId, IReadOnlyList<Guid> orderedImageIds, CancellationToken ct = default)
    {
        var rows = await db.FlightPreparationImages
            .Where(i => i.FlightPreparationId == flightId && orderedImageIds.Contains(i.Id))
            .ToListAsync(ct);
        foreach (var row in rows)
            row.Order = orderedImageIds.ToList().IndexOf(row.Id);
        await db.SaveChangesAsync(ct);
    }

    public async Task<bool> DeleteAsync(Guid flightId, Guid imageId, CancellationToken ct = default)
    {
        var rows = await db.FlightPreparationImages.IgnoreQueryFilters()
            .Where(i => i.Id == imageId && i.FlightPreparationId == flightId)
            .ExecuteDeleteAsync(ct);
        return rows > 0;
    }
}
