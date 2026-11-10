using System.Globalization;
using MediatR;
using Nimbus.Contracts.DTOs.Features.Solar;
using Nimbus.Domain.Services;
namespace Nimbus.Application.Features.Solar.Queries.GetSunTimes;

/// <summary>
///     Sunrise and sunset from the NOAA solar-position algorithm (issue #49). Pure calculation: no provider, nothing stored, so it
///     works offline and is the same answer on every call. The local times use the zone's rules for that date, daylight saving included.
/// </summary>
public sealed class GetSunTimesQueryHandler : IRequestHandler<GetSunTimesQuery, SunTimesDto>
{
    public Task<SunTimesDto> Handle(GetSunTimesQuery request, CancellationToken ct)
    {
        var times = SolarCalculator.Calculate(request.Latitude, request.Longitude, request.Date);
        var condition = times.Condition switch
        {
            SolarCondition.PolarDay => "polarDay",
            SolarCondition.PolarNight => "polarNight",
            _ => "normal"
        };

        string? zoneId = null, sunrise = null, noon = null, sunset = null;
        if (!string.IsNullOrWhiteSpace(request.TimeZone))
        {
            var zone = TimeZoneInfo.FindSystemTimeZoneById(request.TimeZone.Trim());
            zoneId = zone.Id;
            sunrise = Local(times.SunriseUtc, zone);
            noon = Local(times.SolarNoonUtc, zone);
            sunset = Local(times.SunsetUtc, zone);
        }

        return Task.FromResult(new SunTimesDto(request.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), condition,
            times.SunriseUtc, times.SolarNoonUtc, times.SunsetUtc, zoneId, sunrise, noon, sunset));
    }

    private static string Local(DateTime utc, TimeZoneInfo zone) =>
        TimeZoneInfo.ConvertTimeFromUtc(utc, zone).ToString("HH:mm", CultureInfo.InvariantCulture);
}
