using Nimbus.Infrastructure.Http;
namespace Nimbus.Infrastructure.Weather;

/// <summary>Configuration for the Overpass (OpenStreetMap) API (bound from the <c>Overpass</c> section). Its client is registered here; its first caller comes with a later story.</summary>
public sealed class OverpassOptions
{
    public const string SectionName = "Overpass";

    public string BaseUrl { get; init; } = "https://overpass-api.de/api/";

    /// <summary>Overpass queries can be slow, so this is longer than the weather providers'.</summary>
    public int TimeoutSeconds { get; init; } = 25;

    public string UserAgent { get; init; } = "Nimbus/1.0 (flight preparation)";

    public ResilienceOptions Resilience { get; init; } = new();
}
