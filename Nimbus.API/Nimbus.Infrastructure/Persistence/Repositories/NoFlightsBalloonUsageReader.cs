using Nimbus.Domain.Interfaces;
namespace Nimbus.Infrastructure.Persistence.Repositories;

/// <summary>
///     Placeholder: no Flight entity exists yet, so no balloon is ever "in use".
///     TODO(flights): replace with an implementation over the Flight table (owner-scoped, newest first)
///     when the flight feature lands; the delete block and the usage endpoint then work without changes.
/// </summary>
public sealed class NoFlightsBalloonUsageReader : IBalloonUsageReader
{
    public Task<BalloonUsage> GetUsageAsync(Guid ownerId, Guid balloonId, CancellationToken ct = default)
        => Task.FromResult(BalloonUsage.None);
}
