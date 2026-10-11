namespace Nimbus.Contracts.DTOs.Features.Admin;

/// <param name="UserEmail">Null when the attempt did not match any account.</param>
/// <param name="FailureReason">Null for successful attempts; otherwise <c>InvalidPassword</c>, <c>NotApproved</c> or <c>LockedOut</c>.</param>
/// <param name="MaskedIp">Masked client address, e.g. <c>84.198.x.x</c>.</param>
public record LoginEventDto(
    DateTime OccurredAt,
    string? UserEmail,
    bool Succeeded,
    string? MaskedIp,
    string? FailureReason = null
);
