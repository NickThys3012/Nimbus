using Nimbus.Infrastructure.Http;
namespace Nimbus.Infrastructure.Weather;

/// <summary>Configuration for the static map tile server (bound from the <c>StaticMapTiles</c> section). Its client is registered here; its first caller comes with a later story.</summary>
public sealed class StaticMapTilesOptions
{
    public const string SectionName = "StaticMapTiles";

    public string BaseUrl { get; init; } = "https://tile.openstreetmap.org/";

    public int TimeoutSeconds { get; init; } = 10;

    /// <summary>The tile usage policy requires an identifying user agent.</summary>
    public string UserAgent { get; init; } = "Nimbus/1.0 (flight preparation)";

    public ResilienceOptions Resilience { get; init; } = new();
}
