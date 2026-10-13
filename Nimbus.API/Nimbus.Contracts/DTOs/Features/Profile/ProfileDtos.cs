namespace Nimbus.Contracts.DTOs.Features.Profile;

/// <summary>Editable profile values (both optional). The owner is never part of a request.</summary>
public record ProfileRequestDto(string? OperatorName, decimal? PicWeightKg);

/// <summary>The caller's profile; both values are null until set.</summary>
public record ProfileDto(string? OperatorName, decimal? PicWeightKg);
