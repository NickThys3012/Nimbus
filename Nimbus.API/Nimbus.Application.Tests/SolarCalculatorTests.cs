using Nimbus.Domain.Services;
namespace Nimbus.Application.Tests;

/// <summary>
///     Issue #49. The expected sunrise and sunset are independent reference values (Open-Meteo's archive API, queried 2026-11-10 for
///     the dates and places below, in UTC), not output of this calculator. The NOAA calculator itself agrees with such references
///     to within a minute, so that is the tolerance. The published Belgian AIP tables are not available offline: see docs/solar.md.
/// </summary>
public class SolarCalculatorTests
{
    private static DateTime Utc(string s) => DateTime.SpecifyKind(DateTime.Parse(s, System.Globalization.CultureInfo.InvariantCulture), DateTimeKind.Utc);

    // Brussels 50.8503 N 4.3517 E: the equinoxes, both solstices and the mockup's flight date, so every season is covered.
    [TestCase(50.8503, 4.3517, "2025-03-20", "2025-03-20T05:45", "2025-03-20T17:55")]
    [TestCase(50.8503, 4.3517, "2025-06-21", "2025-06-21T03:28", "2025-06-21T20:00")]
    [TestCase(50.8503, 4.3517, "2025-09-22", "2025-09-22T05:29", "2025-09-22T17:40")]
    [TestCase(50.8503, 4.3517, "2025-12-21", "2025-12-21T07:42", "2025-12-21T15:38")]
    [TestCase(50.8503, 4.3517, "2025-08-08", "2025-08-08T04:19", "2025-08-08T19:15")]
    // Weelde, the launch site of the form mockup (about 51.4 N 4.95 E), on the mockup's date.
    [TestCase(51.4, 4.95, "2025-08-08", "2025-08-08T04:15", "2025-08-08T19:15")]
    // Other hemispheres and the date line: the day the event falls on in UTC differs from the local date.
    [TestCase(40.7128, -74.006, "2025-06-21", "2025-06-21T09:25", "2025-06-22T00:30")]
    [TestCase(-33.8688, 151.2093, "2025-12-21", "2025-12-20T18:40", "2025-12-21T09:05")]
    [TestCase(-41.2866, 174.7756, "2025-07-01", "2025-06-30T19:47", "2025-07-01T05:02")]
    [TestCase(64.1466, -21.9426, "2025-12-21", "2025-12-21T11:21", "2025-12-21T15:30")]
    public void MatchesTheReference_WithinOneMinute(double lat, double lon, string date, string sunrise, string sunset)
    {
        var t = SolarCalculator.Calculate(lat, lon, DateOnly.Parse(date));

        Assert.That(t.Condition, Is.EqualTo(SolarCondition.Normal));
        Assert.That((t.SunriseUtc - Utc(sunrise)).TotalMinutes, Is.EqualTo(0).Within(1), "sunrise");
        Assert.That((t.SunsetUtc - Utc(sunset)).TotalMinutes, Is.EqualTo(0).Within(1), "sunset");
    }

    // Reykjavik 64 N at the June solstice: the sun grazes the horizon, so a small difference in method moves the time by minutes. The
    // reference (02:57 / 00:02) and this calculator (02:55 / 00:04) are 2 minutes apart; the one-minute claim is for temperate latitudes.
    [Test]
    public void NearThePolarCircle_AgreesWithinThreeMinutes()
    {
        var t = SolarCalculator.Calculate(64.1466, -21.9426, new DateOnly(2025, 6, 21));

        Assert.That((t.SunriseUtc - Utc("2025-06-21T02:57")).TotalMinutes, Is.EqualTo(0).Within(3));
        Assert.That((t.SunsetUtc - Utc("2025-06-22T00:02")).TotalMinutes, Is.EqualTo(0).Within(3));
    }

    [Test]
    public void SolarNoon_IsBetweenSunriseAndSunset_AndTheDayIsLongestInJune()
    {
        var june = SolarCalculator.Calculate(50.8503, 4.3517, new DateOnly(2025, 6, 21));
        var dec = SolarCalculator.Calculate(50.8503, 4.3517, new DateOnly(2025, 12, 21));

        Assert.That(june.SolarNoonUtc, Is.GreaterThan(june.SunriseUtc).And.LessThan(june.SunsetUtc));
        Assert.That(june.SunsetUtc - june.SunriseUtc, Is.GreaterThan(dec.SunsetUtc - dec.SunriseUtc));
        Assert.That((june.SunsetUtc - june.SunriseUtc).TotalHours, Is.EqualTo(16.53).Within(0.05));
        Assert.That((dec.SunsetUtc - dec.SunriseUtc).TotalHours, Is.EqualTo(7.93).Within(0.05));
    }

    [Test]
    public void AllTimesAreUtc()
    {
        var t = SolarCalculator.Calculate(50.8503, 4.3517, new DateOnly(2025, 3, 20));

        Assert.That(t.SunriseUtc.Kind, Is.EqualTo(DateTimeKind.Utc));
        Assert.That(t.SunsetUtc.Kind, Is.EqualTo(DateTimeKind.Utc));
        Assert.That(t.SolarNoonUtc.Kind, Is.EqualTo(DateTimeKind.Utc));
    }

    // Tromso 69.65 N: the midnight sun in June, the polar night in December.
    [Test]
    public void PolarDay_ReturnsAnEdgeValue_NotNaN()
    {
        var t = SolarCalculator.Calculate(69.6492, 18.9553, new DateOnly(2025, 6, 21));

        Assert.That(t.Condition, Is.EqualTo(SolarCondition.PolarDay));
        Assert.That(t.SunsetUtc - t.SunriseUtc, Is.EqualTo(TimeSpan.FromHours(24)));
        Assert.That(t.SolarNoonUtc - t.SunriseUtc, Is.EqualTo(TimeSpan.FromHours(12)));
    }

    [Test]
    public void PolarNight_ReturnsAnEdgeValue_NotNaN()
    {
        var t = SolarCalculator.Calculate(69.6492, 18.9553, new DateOnly(2025, 12, 21));

        Assert.That(t.Condition, Is.EqualTo(SolarCondition.PolarNight));
        Assert.That(t.SunriseUtc, Is.EqualTo(t.SolarNoonUtc));
        Assert.That(t.SunsetUtc, Is.EqualTo(t.SolarNoonUtc));
    }

    [TestCase(90, 0, 6, 21, SolarCondition.PolarDay)]
    [TestCase(90, 0, 12, 21, SolarCondition.PolarNight)]
    [TestCase(-90, 0, 12, 21, SolarCondition.PolarDay)]
    [TestCase(-90, 0, 6, 21, SolarCondition.PolarNight)]
    public void AtThePoles_ThereIsNoNaN(double lat, double lon, int month, int day, SolarCondition expected)
    {
        var t = SolarCalculator.Calculate(lat, lon, new DateOnly(2025, month, day));

        Assert.That(t.Condition, Is.EqualTo(expected));
        Assert.That(t.SunriseUtc, Is.Not.EqualTo(default(DateTime)));
    }

    [Test]
    public void TheEquator_HasAboutTwelveHoursOfDaylightAllYear()
    {
        foreach (var month in new[] { 3, 6, 9, 12 })
        {
            var t = SolarCalculator.Calculate(0, 10, new DateOnly(2025, month, 21));
            Assert.That((t.SunsetUtc - t.SunriseUtc).TotalHours, Is.EqualTo(12.1).Within(0.1), $"month {month}");
        }
    }

    [Test]
    public void TheOfficialHorizon_Is90Point833Degrees() =>
        Assert.That(SolarCalculator.OfficialZenithDegrees, Is.EqualTo(90.833));

    [TestCase(-91.0, 0.0)]
    [TestCase(91.0, 0.0)]
    [TestCase(0.0, -181.0)]
    [TestCase(0.0, 181.0)]
    [TestCase(double.NaN, 0.0)]
    public void CoordinatesOutOfRange_AreRejected(double lat, double lon) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => SolarCalculator.Calculate(lat, lon, new DateOnly(2025, 6, 21)));

    // Meeus, Astronomical Algorithms, example 7.a: 1957-10-04 19:26:24 UT is JD 2436116.31; at 00:00 UT it is 2436115.5.
    [Test]
    public void JulianDate_MatchesMeeus() =>
        Assert.That(SolarCalculator.JulianDate(new DateOnly(1957, 10, 4)), Is.EqualTo(2436115.5));

    [Test]
    public void JulianDate_OfJ2000_Is2451544Point5At0hUt() =>
        Assert.That(SolarCalculator.JulianDate(new DateOnly(2000, 1, 1)), Is.EqualTo(2451544.5));

    // Intermediate NOAA quantities at 2000-01-01 12:00 UT (T = 0): L0 = 280.46646, M = 357.52911, e = 0.016708634.
    [Test]
    public void NoaaIntermediateQuantities_AtJ2000()
    {
        Assert.That(SolarCalculator.GeometricMeanLongitudeDegrees(0), Is.EqualTo(280.46646).Within(1e-9));
        Assert.That(SolarCalculator.GeometricMeanAnomalyDegrees(0), Is.EqualTo(357.52911).Within(1e-9));
        Assert.That(SolarCalculator.EccentricityEarthOrbit(0), Is.EqualTo(0.016708634).Within(1e-12));
        Assert.That(SolarCalculator.MeanObliquityDegrees(0), Is.EqualTo(23.4392911).Within(1e-6));
    }

    // Meeus example 25.a (1992-10-13 0h TD): declination -7.78507 degrees, equation of time +13.7 minutes.
    [Test]
    public void DeclinationAndEquationOfTime_MatchMeeusExample25a()
    {
        var t = SolarCalculator.JulianCentury(2448908.5);

        Assert.That(SolarCalculator.DeclinationRadians(t) * 180 / Math.PI, Is.EqualTo(-7.785).Within(0.01));
        Assert.That(SolarCalculator.EquationOfTimeMinutes(t), Is.EqualTo(13.7).Within(0.1));
    }
}
