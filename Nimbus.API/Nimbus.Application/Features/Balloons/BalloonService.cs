using Microsoft.Extensions.Logging;
using Nimbus.Application.Common.Exceptions;
using Nimbus.Application.Common.Ownership;
using Nimbus.Domain.Entities;
using Nimbus.Domain.Interfaces;
namespace Nimbus.Application.Features.Balloons;

/// <summary>Owner-scoped balloon operations plus the "registration is unique per owner" rule.</summary>
public interface IBalloonService : IOwnedEntityService<Balloon>
{
    /// <summary>The flights using the balloon, or null when it is missing or not the caller's.</summary>
    Task<BalloonUsage?> GetUsageAsync(Caller caller, Guid id, CancellationToken ct = default);
}

public sealed class BalloonService : OwnedEntityService<Balloon>, IBalloonService
{
    private readonly IBalloonRepository _balloons;
    private readonly IBalloonUsageReader _usage;

    public BalloonService(IBalloonRepository balloons, IBalloonUsageReader usage, ILogger<BalloonService> logger) : base(balloons, logger)
    {
        _balloons = balloons;
        _usage = usage;
    }

    public override async Task<Balloon> CreateAsync(Caller caller, Balloon entity, CancellationToken ct = default)
    {
        await EnsureRegistrationFreeAsync(caller.UserId, entity.Registration, null, ct);
        return await base.CreateAsync(caller, entity, ct);
    }

    public override async Task<OwnedResult> UpdateAsync(Caller caller, Guid id, Balloon incoming, CancellationToken ct = default)
    {
        var existing = await GetAsync(caller, id, ct);
        if (existing is null)
        {
            return OwnedResult.NotFound;
        }

        // Uniqueness is per owner of the balloon, which for an admin is not the caller.
        await EnsureRegistrationFreeAsync(existing.OwnerId, incoming.Registration, id, ct);
        return await base.UpdateAsync(caller, id, incoming, ct);
    }

    public async Task<BalloonUsage?> GetUsageAsync(Caller caller, Guid id, CancellationToken ct = default)
    {
        var balloon = await GetAsync(caller, id, ct);
        return balloon is null ? null : await _usage.GetUsageAsync(balloon.OwnerId, balloon.Id, ct);
    }

    /// <summary>Blocked while any flight uses the balloon, so no logbook entry loses its balloon (docs/balloons.md).</summary>
    public override async Task<OwnedResult> DeleteAsync(Caller caller, Guid id, CancellationToken ct = default)
    {
        var usage = await GetUsageAsync(caller, id, ct);
        if (usage is null)
        {
            return OwnedResult.NotFound;
        }

        if (usage.FlightCount > 0)
        {
            throw new InUseException(
                $"This balloon is used by {usage.FlightCount} flight{(usage.FlightCount == 1 ? "" : "s")} and can't be deleted. Edit it instead if its details changed.");
        }

        return await base.DeleteAsync(caller, id, ct);
    }

    protected override void CopyValues(Balloon target, Balloon source)
    {
        target.Registration = source.Registration;
        target.Type = source.Type;
        target.EnvelopeVolumeM3 = source.EnvelopeVolumeM3;
        target.MaxEnvelopeTemperatureC = source.MaxEnvelopeTemperatureC;
        target.EnvelopeWeightKg = source.EnvelopeWeightKg;
        target.BasketWeightKg = source.BasketWeightKg;
        target.BurnerWeightKg = source.BurnerWeightKg;
        target.CylindersWeightKg = source.CylindersWeightKg;
    }

    private async Task EnsureRegistrationFreeAsync(Guid ownerId, string registration, Guid? excludingId, CancellationToken ct)
    {
        if (await _balloons.RegistrationExistsAsync(ownerId, registration, excludingId, ct))
        {
            throw new DuplicateException(nameof(Balloon.Registration), $"You already have a balloon with this registration.");
        }
    }
}
