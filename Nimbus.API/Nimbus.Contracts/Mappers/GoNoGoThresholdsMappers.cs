using Nimbus.Contracts.DTOs.Features.GoNoGoThresholds;
using Nimbus.Domain.Entities;
namespace Nimbus.Contracts.Mappers;

public static class GoNoGoThresholdsMappers
{
    public static GoNoGoThresholdsDto MapToDto(this GoNoGoThreshold t, bool isCustom) =>
        new(t.WindYellowKt, t.WindRedKt, t.VisibilityYellowKm, t.VisibilityRedKm, t.CapeYellow, t.CapeRed, isCustom);

    public static void ApplyTo(this GoNoGoThresholdsRequestDto r, GoNoGoThreshold target)
    {
        target.WindYellowKt = r.WindYellowKt;
        target.WindRedKt = r.WindRedKt;
        target.VisibilityYellowKm = r.VisibilityYellowKm;
        target.VisibilityRedKm = r.VisibilityRedKm;
        target.CapeYellow = r.CapeYellow;
        target.CapeRed = r.CapeRed;
    }
}
