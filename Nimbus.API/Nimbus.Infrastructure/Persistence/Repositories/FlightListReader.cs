using Microsoft.EntityFrameworkCore;
using Nimbus.Domain.Enums;
using Nimbus.Domain.Interfaces;
using Nimbus.Domain.Services;
namespace Nimbus.Infrastructure.Persistence.Repositories;

/// <summary>
///     Flight list (issue #40). One COUNT, one paged projection (balloon/location/pilot resolved by correlated
///     sub-selects, no entity materialisation) and one query for the page's passenger weights. Filtering, ordering and
///     paging are all SQL. Visibility: own flights plus active shares to the caller; an admin sees every flight.
/// </summary>
public sealed class FlightListReader : IFlightListReader
{
    private readonly AppDbContext _db;

    public FlightListReader(AppDbContext db)
    {
        _db = db;
    }

    public async Task<FlightListPage> GetPageAsync(FlightListCriteria c, CancellationToken ct = default)
    {
        var me = c.UserId;

        // Soft-delete is applied explicitly: filters are ignored so a flight still resolves a balloon, pilot or
        // location its owner has since "deleted" (see docs/flight-preparation.md).
        var visible = _db.FlightPreparations.IgnoreQueryFilters().AsNoTracking().Where(f => !f.IsDeleted);
        if (!c.IsAdmin)
        {
            visible = visible.Where(f => f.OwnerId == me ||
                _db.FlightPreparationShares.IgnoreQueryFilters().Any(s => s.FlightPreparationId == f.Id && s.SharedWithUserId == me && !s.IsDeleted));
        }

        visible = c.Filter switch
        {
            FlightListFilter.Flown => visible.Where(f => f.Status == FlightPreparationStatus.Flown),
            FlightListFilter.NotFlown => visible.Where(f => f.Status != FlightPreparationStatus.Flown),
            FlightListFilter.SharedWithMe => visible.Where(f => f.OwnerId != me),
            _ => visible
        };

        var total = await visible.CountAsync(ct);
        if (total == 0)
        {
            return new FlightListPage([], 0);
        }

        var projected = visible.Select(f => new
        {
            f.Id, f.Title, f.FlightDate, f.FlightTime, f.Status, f.FlownAt, f.OwnerId,
            f.EnvelopeWeightKg, f.BasketWeightKg, f.BurnerWeightKg, f.CylindersWeightKg, f.TotalLiftKg, f.WindSpeedKt, f.VisibilityKm, f.CapeJPerKg,
            Balloon = _db.Balloons.IgnoreQueryFilters().Where(b => b.Id == f.BalloonId && b.OwnerId == f.OwnerId)
                .Select(b => new { b.Registration, b.BasketWeightKg, b.BurnerWeightKg, b.CylindersWeightKg }).FirstOrDefault(),
            LocationName = _db.LaunchLocations.IgnoreQueryFilters().Where(l => l.Id == f.LaunchLocationId && l.OwnerId == f.OwnerId)
                .Select(l => l.Name).FirstOrDefault(),
            PilotWeightKg = _db.Pilots.IgnoreQueryFilters().Where(p => p.Id == f.PilotId && p.OwnerId == f.OwnerId)
                .Select(p => (decimal?)p.WeightKg).FirstOrDefault()
        });

        // Id is the final tie-breaker so paging is stable.
        var ordered = (c.Sort, c.Descending) switch
        {
            (FlightListSort.Balloon, false) => projected.OrderBy(r => r.Balloon!.Registration).ThenByDescending(r => r.FlightDate),
            (FlightListSort.Balloon, true) => projected.OrderByDescending(r => r.Balloon!.Registration).ThenByDescending(r => r.FlightDate),
            (FlightListSort.Location, false) => projected.OrderBy(r => r.LocationName).ThenByDescending(r => r.FlightDate),
            (FlightListSort.Location, true) => projected.OrderByDescending(r => r.LocationName).ThenByDescending(r => r.FlightDate),
            (FlightListSort.Status, false) => projected.OrderBy(r => r.Status).ThenByDescending(r => r.FlightDate),
            (FlightListSort.Status, true) => projected.OrderByDescending(r => r.Status).ThenByDescending(r => r.FlightDate),
            (_, false) => projected.OrderBy(r => r.FlightDate).ThenBy(r => r.FlightTime),
            _ => projected.OrderByDescending(r => r.FlightDate).ThenByDescending(r => r.FlightTime)
        };

        var rows = await ordered.ThenBy(r => r.Id).Skip((c.Page - 1) * c.PageSize).Take(c.PageSize).ToListAsync(ct);

        var ids = rows.Select(r => r.Id).ToList();
        var weights = await _db.Passengers.IgnoreQueryFilters().AsNoTracking()
            .Where(p => !p.IsDeleted && ids.Contains(p.FlightPreparationId))
            .OrderBy(p => p.FlightPreparationId).ThenBy(p => p.Order)
            .Select(p => new { p.FlightPreparationId, p.WeightKg })
            .ToListAsync(ct);
        var byFlight = weights.GroupBy(w => w.FlightPreparationId).ToDictionary(g => g.Key, g => (IReadOnlyList<decimal>)g.Select(w => w.WeightKg).ToList());

        var items = rows.Select(r => new FlightListRow(
            r.Id, r.Title, r.FlightDate, r.FlightTime, r.Balloon?.Registration, r.LocationName, r.Status, r.FlownAt, r.OwnerId,
            new FlightAssessmentInput(
                r.EnvelopeWeightKg ?? 0,
                r.BasketWeightKg ?? r.Balloon?.BasketWeightKg ?? 0, r.BurnerWeightKg ?? r.Balloon?.BurnerWeightKg ?? 0,
                r.CylindersWeightKg ?? r.Balloon?.CylindersWeightKg ?? 0,
                r.PilotWeightKg ?? 0, byFlight.GetValueOrDefault(r.Id, []), r.TotalLiftKg ?? 0, r.WindSpeedKt, r.VisibilityKm, r.CapeJPerKg))).ToList();

        return new FlightListPage(items, total);
    }
}
