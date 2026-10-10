namespace Nimbus.Contracts.DTOs.Features.Admin;

public record AdminUserDto(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    bool IsApproved,
    bool IsRejected,
    bool IsBlocked,
    DateTime? RequestedAt,
    IReadOnlyList<string> Roles
);
