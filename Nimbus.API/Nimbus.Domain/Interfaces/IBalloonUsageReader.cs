namespace Nimbus.Domain.Interfaces;

/// <summary>A flight that uses a balloon, as shown in the "can't be deleted" dialog (e.g. "VR 08 · Weelde").</summary>
public record BalloonFlightRef(string Label, DateOnly Date);

/// <summary>How many of the owner's flights reference a balloon, plus the most recent few.</summary>
public record BalloonUsage(int FlightCount, IReadOnlyList<BalloonFlightRef> RecentFlights)
{
    public static BalloonUsage None { get; } = new(0, []);
}

/// <summary>
///     Tells the balloon feature which flights use a balloon, so deleting a used balloon can be blocked
///     (docs/balloons.md). The flight feature must replace the default no-op implementation with one that
///     queries the Flight table, always filtered by <paramref name="ownerId" />.
/// </summary>
public interface IBalloonUsageReader
{
    Task<BalloonUsage> GetUsageAsync(Guid ownerId, Guid balloonId, CancellationToken ct = default);
}
