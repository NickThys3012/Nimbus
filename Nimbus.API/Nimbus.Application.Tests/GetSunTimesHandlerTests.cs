using Nimbus.Application.Features.Solar.Queries.GetSunTimes;
namespace Nimbus.Application.Tests;

/// <summary>Issue #49: the query turns the calculator's UTC instants into the pilot's local HH:mm, daylight saving included.</summary>
public class GetSunTimesHandlerTests
{
    private static readonly GetSunTimesQueryHandler Handler = new();

    // Brussels on the mockup's date: CEST (UTC+2), so 04:19 UTC is 06:19 and 19:15 UTC is 21:15.
    [Test]
    public async Task InSummer_LocalTimesAreUtcPlusTwo_InBrussels()
    {
        var dto = await Handler.Handle(new GetSunTimesQuery(50.8503, 4.3517, new DateOnly(2025, 8, 8), "Europe/Brussels"), default);

        Assert.That(dto.Sunrise, Is.EqualTo("06:19").Or.EqualTo("06:20").Or.EqualTo("06:18"));
        Assert.That(dto.Sunset, Is.EqualTo("21:15").Or.EqualTo("21:14").Or.EqualTo("21:16"));
        Assert.That(dto.TimeZone, Is.EqualTo("Europe/Brussels"));
        Assert.That(dto.Condition, Is.EqualTo("normal"));
        Assert.That(dto.Date, Is.EqualTo("2025-08-08"));
    }

    [Test]
    public async Task InWinter_LocalTimesAreUtcPlusOne_InBrussels()
    {
        var dto = await Handler.Handle(new GetSunTimesQuery(50.8503, 4.3517, new DateOnly(2025, 12, 21), "Europe/Brussels"), default);

        Assert.That(dto.Sunrise, Is.EqualTo("08:42").Or.EqualTo("08:43").Or.EqualTo("08:41"));
        Assert.That(dto.Sunset, Is.EqualTo("16:38").Or.EqualTo("16:39").Or.EqualTo("16:37"));
    }

    [Test]
    public async Task WithoutAZone_OnlyUtcIsReturned()
    {
        var dto = await Handler.Handle(new GetSunTimesQuery(50.8503, 4.3517, new DateOnly(2025, 6, 21)), default);

        Assert.That(dto.Sunrise, Is.Null);
        Assert.That(dto.Sunset, Is.Null);
        Assert.That(dto.TimeZone, Is.Null);
        Assert.That(dto.SunriseUtc, Is.LessThan(dto.SolarNoonUtc));
        Assert.That(dto.SolarNoonUtc, Is.LessThan(dto.SunsetUtc));
    }

    [Test]
    public async Task APolarDay_IsReportedAsSuch()
    {
        var dto = await Handler.Handle(new GetSunTimesQuery(69.6492, 18.9553, new DateOnly(2025, 6, 21), "Europe/Oslo"), default);

        Assert.That(dto.Condition, Is.EqualTo("polarDay"));
    }

    [Test]
    public async Task APolarNight_IsReportedAsSuch()
    {
        var dto = await Handler.Handle(new GetSunTimesQuery(69.6492, 18.9553, new DateOnly(2025, 12, 21)), default);

        Assert.That(dto.Condition, Is.EqualTo("polarNight"));
    }

    [Test]
    public void Validator_RejectsBadCoordinatesDatesAndZones()
    {
        var v = new GetSunTimesQueryValidator();
        var ok = new DateOnly(2025, 6, 21);

        Assert.That(v.Validate(new GetSunTimesQuery(51, 4, ok, "Europe/Brussels")).IsValid, Is.True);
        Assert.That(v.Validate(new GetSunTimesQuery(51, 4, ok)).IsValid, Is.True);
        Assert.That(v.Validate(new GetSunTimesQuery(91, 4, ok)).IsValid, Is.False);
        Assert.That(v.Validate(new GetSunTimesQuery(51, 181, ok)).IsValid, Is.False);
        Assert.That(v.Validate(new GetSunTimesQuery(51, 4, new DateOnly(1800, 1, 1))).IsValid, Is.False);
        Assert.That(v.Validate(new GetSunTimesQuery(51, 4, ok, "Mars/Olympus")).IsValid, Is.False);
    }
}
