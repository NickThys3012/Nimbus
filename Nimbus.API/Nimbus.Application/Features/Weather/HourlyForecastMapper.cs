using System.Globalization;
using Nimbus.Application.Common.Interfaces;
using Nimbus.Contracts.DTOs.Features.Weather;
namespace Nimbus.Application.Features.Weather;

/// <summary>
///     Forecast models the pilot can choose for the hourly forecast (issue #45), and how a seamless model is split into the
///     models it is made of, best resolution first. Splitting lets each hour record which model it came from.
/// </summary>
public static class HourlyForecastModels
{
    public const string Default = "icon_seamless";

    /// <summary>The models probed for a launch site (issue #45). Models the provider rejected or that return no data are not offered.</summary>
    public static readonly IReadOnlyList<string> Supported =
    [
        "icon_seamless", "icon_d2", "icon_eu", "icon_global",
        "meteofrance_seamless", "meteofrance_arome_france", "meteofrance_arpege_europe", "meteofrance_arpege_world",
        "cma_grapes_global",
        "ukmo_seamless", "ukmo_uk_deterministic_2km", "ukmo_global_deterministic_10km",
        "best_match",
        "knmi_seamless", "knmi_harmonie_arome_europe",
        "dmi_seamless", "dmi_harmonie_arome_europe",
        "metno_seamless",
        "ecmwf_ifs", "ecmwf_ifs025", "ecmwf_aifs025_single",
        "gfs_seamless", "gfs_global",
        "gem_seamless", "gem_global", "gem_regional",
        "jma_seamless", "jma_gsm"
    ];

    private static readonly IReadOnlyDictionary<string, string[]> Components = new Dictionary<string, string[]>
    {
        ["icon_seamless"] = ["icon_d2", "icon_eu", "icon_global"],
        ["meteofrance_seamless"] = ["meteofrance_arome_france", "meteofrance_arpege_europe", "meteofrance_arpege_world"],
        ["ukmo_seamless"] = ["ukmo_uk_deterministic_2km", "ukmo_global_deterministic_10km"]
    };

    /// <summary>The models to ask the provider for: a known seamless model is split into its parts, any other model is asked for as is.</summary>
    public static IReadOnlyList<string> ComponentsOf(string model) => Components.TryGetValue(model, out var parts) ? parts : [model];
}

/// <summary>Turns the provider's per-model hours into one series: each hour from the best-resolution model that has data for it.</summary>
public static class HourlyForecastMapper
{
    public static HourlyForecastDto Map(string model, IReadOnlyList<string> components, RawHourlyForecast raw)
    {
        var hours = new List<HourlyForecastHourDto>();
        foreach (var hour in raw.Hours)
            foreach (var component in components)
            {
                // A model past its horizon returns nothing for an hour (all null): the next, coarser model takes over.
                if (!hour.ByModel.TryGetValue(component, out var v) || !HasData(v)) continue;
                hours.Add(new HourlyForecastHourDto(hour.Time, component,
                    Round(v.TemperatureC, 1), Round(v.WindSpeedKt, 1), Direction(v.WindDirectionDeg), Round(v.WindGustKt, 1),
                    Round(v.CapeJPerKg, 0), Round(v.PrecipitationMm, 1), Percent(v.PrecipitationProbabilityPct)));
                break;
            }

        var days = raw.Days.Select(d => new ForecastDayDto(d.Date, TimeOf(d.Sunrise), TimeOf(d.Sunset))).ToList();
        return new HourlyForecastDto(model, raw.TimeZone, raw.UtcOffsetSeconds, days, hours);
    }

    private static bool HasData(ForecastValues v) => v.WindSpeedKt is not null || v.TemperatureC is not null;

    /// <summary>The <c>HH:mm</c> part of the provider's <c>yyyy-MM-ddTHH:mm</c>; null for anything else (polar day or night).</summary>
    private static string? TimeOf(string? local) =>
        local is { Length: 16 } && local[10] == 'T' ? local[11..] : null;

    private static decimal? Round(double? v, int digits) =>
        v is { } d && double.IsFinite(d) ? Math.Round((decimal)d, digits, MidpointRounding.AwayFromZero) : null;

    private static int? Direction(double? v) =>
        v is { } d && double.IsFinite(d) ? (int)(((Math.Round(d, MidpointRounding.AwayFromZero) % 360) + 360) % 360) : null;

    private static int? Percent(double? v) =>
        v is { } d && double.IsFinite(d) ? (int)Math.Clamp(Math.Round(d, MidpointRounding.AwayFromZero), 0, 100) : null;
}
