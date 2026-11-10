using Nimbus.Application.Features.Weather;
namespace Nimbus.Application.Tests;

/// <summary>Issue #46: pressure level to altitude, and the mapping of raw values to ordered levels.</summary>
public class WindProfileMapperTests
{
    private static Dictionary<string, double?> Full(bool geopotential = true)
    {
        var v = new Dictionary<string, double?> { ["temperature_2m"] = 15 };
        foreach (var h in WindProfileMapper.LowLevelsAglM) { v[$"wind_speed_{h}m"] = 5 + h / 100.0; v[$"wind_direction_{h}m"] = 270; }
        foreach (var (p, g) in new[] { (950, 540.0), (925, 765.0), (900, 990.0), (850, 1460.0) })
        {
            v[$"wind_speed_{p}hPa"] = 12; v[$"wind_direction_{p}hPa"] = 280; v[$"temperature_{p}hPa"] = 10;
            v[$"geopotential_height_{p}hPa"] = geopotential ? g : null;
        }
        return v;
    }

    [TestCase(950, 500)]
    [TestCase(925, 800)]
    [TestCase(900, 1000)]
    [TestCase(850, 1500)]
    public void WithoutAGeopotentialHeight_TheStandardAtmosphereAltitudeIsUsed(int hpa, int expected)
    {
        var (alt, source) = WindProfileMapper.PressureLevelAltitude(hpa, null);

        Assert.That(alt, Is.EqualTo(expected));
        Assert.That(source, Is.EqualTo("standard"));
    }

    [TestCase(950)]
    [TestCase(925)]
    [TestCase(900)]
    [TestCase(850)]
    public void TheFallbackAltitudes_AgreeWithTheInternationalStandardAtmosphere_WithinAHundredMetres(int hpa)
    {
        var isa = 44330.77 * (1 - Math.Pow(hpa / 1013.25, 0.190263));

        Assert.That(WindProfileMapper.StandardAltitudeM[hpa], Is.EqualTo(isa).Within(100));
    }

    [Test]
    public void AGeopotentialHeight_WinsOverTheStandardValue_AndIsRounded()
    {
        var (alt, source) = WindProfileMapper.PressureLevelAltitude(950, 543.6);

        Assert.That((alt, source), Is.EqualTo((544, "geopotential")));
    }

    [Test]
    public void FullData_GivesTenOrderedLevels_LowLevelsFirst_AndPressureLevelsAtTheirOwnAltitude()
    {
        var m = WindProfileMapper.Map(Full());

        Assert.That(m.Missing, Is.Empty);
        Assert.That(m.Levels.Select(l => l.AltitudeM), Is.EqualTo(new[] { 10, 20, 50, 100, 150, 200, 540, 765, 990, 1460 }));
        Assert.That(m.Levels.Take(6).Select(l => l.AltitudeSource), Is.All.EqualTo("agl"));
        Assert.That(m.Levels.Skip(6).Select(l => l.AltitudeSource), Is.All.EqualTo("geopotential"));
    }

    [Test]
    public void MissingGeopotentialHeights_FallBackToTheStandardAltitudes()
    {
        var m = WindProfileMapper.Map(Full(geopotential: false));

        Assert.That(m.Levels.Skip(6).Select(l => l.AltitudeM), Is.EqualTo(new[] { 500, 800, 1000, 1500 }));
        Assert.That(m.Levels.Skip(6).Select(l => l.AltitudeSource), Is.All.EqualTo("standard"));
    }

    [Test]
    public void ALevelMissingFromTheModel_IsReported_NeverZeroFilled()
    {
        var v = Full();
        v["wind_speed_950hPa"] = null;
        v["wind_direction_950hPa"] = null;
        v.Remove("wind_speed_200m");

        var m = WindProfileMapper.Map(v);

        Assert.That(m.Missing, Is.EquivalentTo(new[] { "950 hPa", "200 m" }));
        Assert.That(m.Levels.Count, Is.EqualTo(8));
        Assert.That(m.Levels.All(l => l.SpeedKt > 0), Is.True);
    }

    [Test]
    public void DirectionIsNormalisedTo0To359_AndSpeedAndTemperatureAreRounded()
    {
        var v = Full();
        v["wind_direction_10m"] = 360;
        v["wind_speed_10m"] = 7.26;

        var l = WindProfileMapper.Map(v).Levels[0];

        Assert.That(l.DirectionDeg, Is.EqualTo(0));
        Assert.That(l.SpeedKt, Is.EqualTo(7.3m));
        Assert.That(l.TemperatureC, Is.EqualTo(14.9m)); // 15 - 0.0065 * 8, rounded
    }

    [Test]
    public void EveryRequestedVariable_IsOneTheMapperReads()
    {
        Assert.That(WindProfileMapper.RequestedVariables, Has.Member("geopotential_height_950hPa"));
        Assert.That(WindProfileMapper.RequestedVariables, Has.Member("wind_direction_200m"));
        Assert.That(WindProfileMapper.RequestedVariables, Is.Unique);
    }
}
