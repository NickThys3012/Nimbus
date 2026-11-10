using Nimbus.Infrastructure.Http;
namespace Nimbus.Infrastructure.Weather;

/// <summary>
///     Configuration for aviationweather.gov (bound from the <c>AviationWeather</c> section). The address, the timeout and the
///     user agent are configuration, never literals in the client, so each can change without a code change.
/// </summary>
public sealed class AviationWeatherOptions
{
    public const string SectionName = "AviationWeather";

    /// <summary>Base address of the Data API. A trailing slash is added when missing.</summary>
    public string BaseUrl { get; init; } = "https://aviationweather.gov/api/data/";

    /// <summary>How long one attempt may take before it counts as failed. The form waits on this, so it stays short.</summary>
    public int TimeoutSeconds { get; init; } = 8;

    /// <summary>Sent on every request; the provider asks clients to identify themselves.</summary>
    public string UserAgent { get; init; } = "Nimbus/1.0 (flight preparation)";

    /// <summary>Retries and circuit breaker.</summary>
    public ResilienceOptions Resilience { get; init; } = new();
}
