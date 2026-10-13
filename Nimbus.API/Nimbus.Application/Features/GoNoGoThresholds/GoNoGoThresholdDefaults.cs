using Nimbus.Domain.Entities;
namespace Nimbus.Application.Features.GoNoGoThresholds;

/// <summary>Built-in defaults (issue #27) used when neither the user nor the global default row has values.</summary>
public static class GoNoGoThresholdDefaults
{
    public static GoNoGoThreshold BuiltIn() => new()
    {
        WindYellowKt = 10m, WindRedKt = 15m,
        VisibilityYellowKm = 5m, VisibilityRedKm = 3m,
        CapeYellow = 300m, CapeRed = 500m,
    };
}
