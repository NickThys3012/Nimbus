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
