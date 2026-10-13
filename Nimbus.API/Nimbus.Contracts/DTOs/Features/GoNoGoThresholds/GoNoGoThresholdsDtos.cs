namespace Nimbus.Contracts.DTOs.Features.GoNoGoThresholds;

/// <summary>Editable limits. The owner is never part of a request.</summary>
public record GoNoGoThresholdsRequestDto(
    decimal WindYellowKt, decimal WindRedKt,
    decimal VisibilityYellowKm, decimal VisibilityRedKm,
    decimal CapeYellow, decimal CapeRed);

/// <summary>The effective limits of the caller. <paramref name="IsCustom" /> is false when the defaults apply.</summary>
public record GoNoGoThresholdsDto(
    decimal WindYellowKt, decimal WindRedKt,
    decimal VisibilityYellowKm, decimal VisibilityRedKm,
    decimal CapeYellow, decimal CapeRed,
    bool IsCustom);
