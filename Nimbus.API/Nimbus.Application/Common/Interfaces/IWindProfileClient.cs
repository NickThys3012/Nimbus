namespace Nimbus.Application.Common.Interfaces;

/// <summary>
///     The provider's values for one hour, keyed by Open-Meteo variable name (for example <c>wind_speed_950hPa</c>). A
///     variable the model does not provide is null, never zero.
/// </summary>
public sealed record RawWindProfile(IReadOnlyDictionary<string, double?> Values);

/// <summary>Pressure-level and low-altitude wind data from the forecast provider (Open-Meteo).</summary>
public interface IWindProfileClient
{
    /// <summary>
    ///     Fetches the wind variables listed in <see cref="Features.Weather.WindProfileMapper.RequestedVariables" /> for one
    ///     UTC hour and one model. Never throws for a provider problem: that is a failed result, logged by the client.
    /// </summary>
    Task<ProviderResult<RawWindProfile>> GetAsync(double latitude, double longitude, DateTime hourUtc, string model, CancellationToken ct = default);
}

/// <summary>One hour of a wind series: the provider's values for that UTC hour, keyed like <see cref="RawWindProfile" />. A variable the model lacks is null.</summary>
public sealed record RawWindHour(DateTime HourUtc, IReadOnlyDictionary<string, double?> Values);

public sealed record RawWindSeries(IReadOnlyList<RawWindHour> Hours);

/// <summary>Hourly pressure-level and low-altitude wind data over a window (issue #47), for the trajectory simulator.</summary>
public interface IWindSeriesClient
{
    /// <summary>
    ///     Fetches the wind variables for every UTC hour from <paramref name="fromUtc" /> to <paramref name="toUtc" />
    ///     (inclusive) for one model, in one request. Never throws for a provider problem: that is a failed result, logged by the client.
    /// </summary>
    Task<ProviderResult<RawWindSeries>> GetSeriesAsync(double latitude, double longitude, DateTime fromUtc, DateTime toUtc, string model, CancellationToken ct = default);
}
