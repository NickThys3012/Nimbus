namespace Nimbus.Contracts.DTOs.Features.Weather;

/// <summary>
///     One forecast hour. <paramref name="Time" /> is the launch site's local wall-clock time (<c>yyyy-MM-ddTHH:mm</c>, no
///     offset) so the UI can show it as is and match it to the flight's planned time. <paramref name="Model" /> is the model
///     this hour came from, so the UI can show where a seamless model drops to a coarser one. A value the model does not
///     provide is null, never zero.
/// </summary>
public record HourlyForecastHourDto(
    string Time,
    string Model,
    decimal? TemperatureC,
    decimal? WindSpeedKt,
    int? WindDirectionDeg,
    decimal? WindGustKt,
    decimal? CapeJPerKg,
    decimal? PrecipitationMm,
    int? PrecipitationProbabilityPct);

/// <summary>Sunrise and sunset of one local day (<c>HH:mm</c> in the launch site's timezone, null when the sun does not rise or set).</summary>
public record ForecastDayDto(string Date, string? Sunrise, string? Sunset);

/// <summary>
///     The hourly forecast for a place: the requested <paramref name="Model" />, the site's IANA <paramref name="TimeZone" />
///     (derived from the coordinates) and its offset from UTC, the days with sunrise and sunset, and the hours. Fetched, never stored.
/// </summary>
public record HourlyForecastDto(
    string Model,
    string TimeZone,
    int UtcOffsetSeconds,
    IReadOnlyList<ForecastDayDto> Days,
    IReadOnlyList<HourlyForecastHourDto> Hours);
