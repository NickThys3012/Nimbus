using Microsoft.EntityFrameworkCore;
using Nimbus.Domain.Entities;
using Nimbus.Domain.Interfaces;
using Nimbus.Domain.Services;
namespace Nimbus.Infrastructure.Persistence.Repositories;

/// <summary>
///     Read-only view of one flight (issue #41). The access decision is made here, in SQL terms, never by the UI:
///     the owner, an admin and anyone with an active share may read; everyone else gets an explicit "exists but not
///     yours" answer so the API can say 403 rather than pretend the flight is missing. Names resolve from the
///     flight owner's reference data, including entries the owner has since soft-deleted, because the viewer's own
///     balloons, pilots and locations are a different set.
/// </summary>
public sealed class FlightDetailReader : IFlightDetailReader
{
    private readonly AppDbContext _db;

    public FlightDetailReader(AppDbContext db)
    {
        _db = db;
    }

    public async Task<FlightAccess> GetAccessAsync(Guid viewerId, bool isAdmin, Guid flightId, CancellationToken ct = default)
    {
        var owner = await _db.FlightPreparations.IgnoreQueryFilters().AsNoTracking()
            .Where(f => f.Id == flightId && !f.IsDeleted)
            .Select(f => (Guid?)f.OwnerId)
            .FirstOrDefaultAsync(ct);
        return owner is null ? new FlightAccess(false, null) : new FlightAccess(true, await RoleAsync(viewerId, isAdmin, flightId, owner.Value, ct));
    }

    public async Task<FlightDetailResult> GetAsync(Guid viewerId, bool isAdmin, Guid flightId, CancellationToken ct = default)
    {
        var access = await GetAccessAsync(viewerId, isAdmin, flightId, ct);
        if (access.Role is not { } role)
            return new FlightDetailResult(access, null);

        // Children are filtered explicitly because the root query ignores the global soft-delete filter.
        var flight = await _db.FlightPreparations.IgnoreQueryFilters().AsNoTracking()
            .Include(f => f.Passengers.Where(p => !p.IsDeleted).OrderBy(p => p.Order))
            .Include(f => f.WindLevels.Where(w => !w.IsDeleted).OrderBy(w => w.Order))
            .Include(f => f.Images.Where(i => !i.IsDeleted).OrderBy(i => i.Order))
            .AsSplitQuery()
            .FirstAsync(f => f.Id == flightId, ct);

        var balloon = flight.BalloonId is { } balloonId
            ? await _db.Balloons.IgnoreQueryFilters().AsNoTracking()
                .Where(b => b.Id == balloonId && b.OwnerId == flight.OwnerId)
                .Select(b => new { b.Registration, b.BasketWeightKg, b.BurnerWeightKg, b.CylindersWeightKg })
                .FirstOrDefaultAsync(ct)
            : null;
        var pilot = flight.PilotId is { } pilotId
            ? await _db.Pilots.IgnoreQueryFilters().AsNoTracking()
                .Where(p => p.Id == pilotId && p.OwnerId == flight.OwnerId)
                .Select(p => new { p.Name, p.WeightKg })
                .FirstOrDefaultAsync(ct)
            : null;
        var location = flight.LaunchLocationId is { } locationId
            ? await _db.LaunchLocations.IgnoreQueryFilters().AsNoTracking()
                .Where(l => l.Id == locationId && l.OwnerId == flight.OwnerId)
                .Select(l => new { l.Name, l.IcaoCode })
                .FirstOrDefaultAsync(ct)
            : null;

        var assessment = FlightAssessmentInput.From(
            flight, pilot?.WeightKg ?? 0, balloon?.BasketWeightKg ?? 0, balloon?.BurnerWeightKg ?? 0, balloon?.CylindersWeightKg ?? 0);
        return new FlightDetailResult(access, new FlightDetailData(
            flight, role, balloon?.Registration, pilot?.Name, location?.Name, location?.IcaoCode, assessment));
    }

    private async Task<FlightViewerRole?> RoleAsync(Guid viewerId, bool isAdmin, Guid flightId, Guid ownerId, CancellationToken ct)
    {
        if (ownerId == viewerId) return FlightViewerRole.Owner;
        if (isAdmin) return FlightViewerRole.Admin;
        var shared = await _db.FlightPreparationShares.IgnoreQueryFilters().AsNoTracking()
            .AnyAsync(s => s.FlightPreparationId == flightId && s.SharedWithUserId == viewerId && !s.IsDeleted, ct);
        return shared ? FlightViewerRole.Shared : null;
    }
}
