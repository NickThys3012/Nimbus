namespace Nimbus.Application.Common.Interfaces;

/// <summary>The provider's text for one station, exactly as returned. Either part is null when the provider has nothing for it.</summary>
public sealed record RawAviationWeather(string? MetarText, string? TafText);

/// <summary>Observations and forecasts from the aviation weather provider (aviationweather.gov).</summary>
public interface IAviationWeatherClient
{
    /// <summary>
    ///     Fetches the raw METAR and TAF text for an ICAO station. Never throws for a provider problem (unreachable, timeout,
    ///     error status): that is a failed result, and the failure is logged by the client. Only the caller's own cancellation
    ///     propagates.
    /// </summary>
    Task<ProviderResult<RawAviationWeather>> GetRawAsync(string icaoCode, CancellationToken ct = default);
}
