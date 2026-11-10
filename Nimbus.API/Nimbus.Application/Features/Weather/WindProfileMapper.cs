using Nimbus.Contracts.DTOs.Features.Weather;
namespace Nimbus.Application.Features.Weather;

/// <summary>Result of mapping raw provider values: the ordered levels, plus the names of any required values the model did not return.</summary>
public sealed record WindProfileMapping(IReadOnlyList<WindProfileLevelDto> Levels, IReadOnlyList<string> Missing);

/// <summary>
///     Turns Open-Meteo pressure-level and low-altitude values into ordered wind levels (issue #46). A level with any
///     required value missing is reported in <see cref="WindProfileMapping.Missing" /> and left out: it is never filled with zero.
/// </summary>
public static class WindProfileMapper
{
    public const string DefaultModel = "icon_seamless";

    /// <summary>Models verified (issue #46) to return every pressure level and the low-altitude winds. Anything else is rejected.</summary>
    public static readonly IReadOnlyList<string> SupportedModels =
    [
        "icon_seamless", "icon_d2", "icon_eu", "icon_global",
        "meteofrance_seamless", "meteofrance_arome_france", "meteofrance_arpege_europe", "meteofrance_arpege_world",
        "cma_grapes_global", "ukmo_uk_deterministic_2km", "best_match"
    ];

    /// <summary>Metres above ground.</summary>
    public static readonly IReadOnlyList<int> LowLevelsAglM = [10, 20, 50, 100, 150, 200];

    /// <summary>Standard-atmosphere altitudes (m) used when the model returns no geopotential height.</summary>
    public static readonly IReadOnlyDictionary<int, int> StandardAltitudeM = new Dictionary<int, int>
    {
        [950] = 500, [925] = 800, [900] = 1000, [850] = 1500
    };

    /// <summary>The pressure levels (hPa), lowest altitude first.</summary>
    public static IReadOnlyList<int> PressureLevelsHpa => [950, 925, 900, 850];

    /// <summary>Every hourly variable the mapper reads: the request asks for exactly these.</summary>
    public static IReadOnlyList<string> RequestedVariables { get; } =
        [.. LowLevelsAglM.Select(h => $"wind_speed_{h}m"),
         .. LowLevelsAglM.Select(h => $"wind_direction_{h}m"),
         "temperature_2m",
         .. PressureLevelsHpa.SelectMany(p => new[] { $"wind_speed_{p}hPa", $"wind_direction_{p}hPa", $"geopotential_height_{p}hPa", $"temperature_{p}hPa" })];

    /// <summary>Altitude (m) of a pressure level: the model's geopotential height when present, else the standard-atmosphere value.</summary>
    public static (int AltitudeM, string Source) PressureLevelAltitude(int hpa, double? geopotentialHeightM)
    {
        if (geopotentialHeightM is { } h && double.IsFinite(h) && h > 0)
            return ((int)Math.Round(h, MidpointRounding.AwayFromZero), "geopotential");
        return (StandardAltitudeM[hpa], "standard");
    }

    public static WindProfileMapping Map(IReadOnlyDictionary<string, double?> values)
    {
        double? Get(string key) => values.TryGetValue(key, out var v) && v is { } d && double.IsFinite(d) ? d : null;

        var levels = new List<WindProfileLevelDto>();
        var missing = new List<string>();
        var t2m = Get("temperature_2m");

        foreach (var h in LowLevelsAglM)
        {
            var speed = Get($"wind_speed_{h}m");
            var dir = Get($"wind_direction_{h}m");
            if (speed is null || dir is null || t2m is null)
            {
                missing.Add($"{h} m");
                continue;
            }

            // The provider has no temperature at these heights: standard lapse rate (6.5 K/km) from the 2 m value.
            var temp = t2m.Value - 0.0065 * (h - 2);
            levels.Add(Build(h, dir.Value, speed.Value, temp, "agl"));
        }

        foreach (var p in PressureLevelsHpa)
        {
            var speed = Get($"wind_speed_{p}hPa");
            var dir = Get($"wind_direction_{p}hPa");
            var temp = Get($"temperature_{p}hPa");
            if (speed is null || dir is null || temp is null)
            {
                missing.Add($"{p} hPa");
                continue;
            }

            var (alt, source) = PressureLevelAltitude(p, Get($"geopotential_height_{p}hPa"));
            levels.Add(Build(alt, dir.Value, speed.Value, temp.Value, source));
        }

        return new WindProfileMapping([.. levels.OrderBy(l => l.AltitudeM)], missing);
    }

    private static WindProfileLevelDto Build(int altitudeM, double dir, double speedKt, double tempC, string source)
    {
        var deg = ((int)Math.Round(dir, MidpointRounding.AwayFromZero) % 360 + 360) % 360;
        return new WindProfileLevelDto(altitudeM, deg, (decimal)Math.Round(speedKt, 1), (decimal)Math.Round(tempC, 1), source);
    }
}
