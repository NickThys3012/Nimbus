namespace Nimbus.Contracts.DTOs.Features.GoNoGoThresholds;

/// <summary>Verdict for the supplied weather values against the caller's thresholds: "green", "yellow", "red" or "unknown".</summary>
public record GoNoGoVerdictDto(string Verdict);
