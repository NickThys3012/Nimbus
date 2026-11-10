namespace Nimbus.Application.Common.Interfaces;

/// <summary>Configured defaults for the hourly forecast: the model used when a request names none, and how many days it covers.</summary>
public interface IForecastSettings
{
    string DefaultModel { get; }

    int Days { get; }

    /// <summary>How long a fetched wind series is kept, in minutes. Shorter than the models' update cycles (3 to 6 h).</summary>
    int SeriesCacheMinutes { get; }
}

/// <summary>The values of one model for one hour. Anything the model does not provide is null, never zero.</summary>
public sealed record ForecastValues(
    double? TemperatureC, double? WindSpeedKt, double? WindDirectionDeg, double? WindGustKt,
    double? CapeJPerKg, double? PrecipitationMm, double? PrecipitationProbabilityPct);

/// <summary>One hour (local time <c>yyyy-MM-ddTHH:mm</c>) with the values of each component model that was asked for.</summary>
public sealed record RawForecastHour(string Time, IReadOnlyDictionary<string, ForecastValues> ByModel);

/// <summary>Sunrise and sunset of one local day, as the provider's local <c>yyyy-MM-ddTHH:mm</c> text (null when absent).</summary>
public sealed record RawForecastDay(string Date, string? Sunrise, string? Sunset);

public sealed record RawHourlyForecast(string TimeZone, int UtcOffsetSeconds, IReadOnlyList<RawForecastHour> Hours, IReadOnlyList<RawForecastDay> Days);

/// <summary>Hourly forecast and daily sunrise/sunset from the forecast provider (Open-Meteo).</summary>
public interface IHourlyForecastClient
{
    /// <summary>
    ///     Fetches the hourly forecast for <paramref name="days" /> days, once per component model in
    ///     <paramref name="models" />, in the site's own timezone (derived by the provider from the coordinates). Never throws for a
    ///     provider problem: that is a failed result, logged by the client.
    /// </summary>
    Task<ProviderResult<RawHourlyForecast>> GetAsync(double latitude, double longitude, IReadOnlyList<string> models, int days, CancellationToken ct = default);
}
