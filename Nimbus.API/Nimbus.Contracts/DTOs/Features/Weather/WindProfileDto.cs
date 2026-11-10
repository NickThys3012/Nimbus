namespace Nimbus.Contracts.DTOs.Features.Weather;

/// <summary>
///     One altitude of the fetched wind profile. Same shape as a stored wind level plus where the altitude came from:
///     <c>agl</c> (metres above ground, the low levels), <c>geopotential</c> (the model's own height for the pressure level)
///     or <c>standard</c> (standard-atmosphere fallback because the model returned no height).
/// </summary>
public record WindProfileLevelDto(int AltitudeM, int DirectionDeg, decimal SpeedKt, decimal TemperatureC, string AltitudeSource);

/// <summary>The wind by altitude for one place and hour, lowest altitude first. Fetched, never stored.</summary>
public record WindProfileDto(string Model, DateTime ForecastHourUtc, IReadOnlyList<WindProfileLevelDto> Levels);
