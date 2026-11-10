namespace Nimbus.Contracts.DTOs.Features.Solar;

/// <summary>
///     Sunrise and sunset at a place on a day, by the NOAA solar-position algorithm. The UTC instants are always present. The
///     <c>HH:mm</c> local times are present when a time zone was asked for. <paramref name="Condition" /> is <c>normal</c>,
///     <c>polarDay</c> (the sun does not set) or <c>polarNight</c> (it does not rise); in the polar cases the times are edge values,
///     not real events. Calculated, never stored.
/// </summary>
public record SunTimesDto(
    string Date,
    string Condition,
    DateTime SunriseUtc,
    DateTime SolarNoonUtc,
    DateTime SunsetUtc,
    string? TimeZone,
    string? Sunrise,
    string? SolarNoon,
    string? Sunset);
