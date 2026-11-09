using Nimbus.Domain.Entities;
using Nimbus.Domain.Enums;
namespace Nimbus.Domain.Services;

/// <summary>
///     The limits the evaluator compares against. Always supplied by the caller from the user's settings
///     (own row, else default row); the evaluator itself contains no threshold values.
/// </summary>
public readonly record struct GoNoGoLimits(
    decimal WindYellowKt, decimal WindRedKt,
    decimal VisibilityYellowKm, decimal VisibilityRedKm,
    decimal CapeYellow, decimal CapeRed)
{
    public static GoNoGoLimits From(GoNoGoThreshold t) =>
        new(t.WindYellowKt, t.WindRedKt, t.VisibilityYellowKm, t.VisibilityRedKm, t.CapeYellow, t.CapeRed);
}

/// <summary>
///     Pure Go/No-Go decision (issue #38). Rules, per metric:
///     wind: yellow at or above the yellow limit, red at or above the red limit;
///     visibility: yellow at or below the yellow limit, red at or below the red limit;
///     CAPE: yellow at or above the yellow limit, red at or above the red limit.
///     Boundaries are inclusive (exactly at a limit already triggers that level).
///     Red wins over yellow over green. A missing metric is skipped; when all three are missing the
///     result is <see cref="GoNoGoVerdict.Unknown" />, never green. Holds no user data and reads no storage.
/// </summary>
public static class GoNoGoEvaluator
{
    /// <summary>Full-flight entry point: reads the meteo fields of the flight preparation.</summary>
    public static GoNoGoVerdict Evaluate(FlightPreparation flight, GoNoGoLimits limits)
    {
        ArgumentNullException.ThrowIfNull(flight);
        return Evaluate(flight.WindSpeedKt, flight.VisibilityKm, flight.CapeJPerKg, limits);
    }

    /// <summary>Raw-values entry point for lightweight projections (e.g. the flight list).</summary>
    public static GoNoGoVerdict Evaluate(decimal? windSpeedKt, decimal? visibilityKm, decimal? capeJPerKg, GoNoGoLimits limits)
    {
        if (windSpeedKt is null && visibilityKm is null && capeJPerKg is null)
        {
            return GoNoGoVerdict.Unknown;
        }

        var result = GoNoGoVerdict.Green;
        if (windSpeedKt is { } w)
        {
            result = Max(result, Level(w >= limits.WindRedKt, w >= limits.WindYellowKt));
        }

        if (visibilityKm is { } v)
        {
            result = Max(result, Level(v <= limits.VisibilityRedKm, v <= limits.VisibilityYellowKm));
        }

        if (capeJPerKg is { } c)
        {
            result = Max(result, Level(c >= limits.CapeRed, c >= limits.CapeYellow));
        }

        return result;
    }

    private static GoNoGoVerdict Level(bool red, bool yellow) =>
        red ? GoNoGoVerdict.Red : yellow ? GoNoGoVerdict.Yellow : GoNoGoVerdict.Green;

    private static GoNoGoVerdict Max(GoNoGoVerdict a, GoNoGoVerdict b) => a > b ? a : b;
}
