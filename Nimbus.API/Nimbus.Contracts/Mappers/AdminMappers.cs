using Nimbus.Contracts.DTOs.Features.Admin;
using Nimbus.Domain.Entities;
namespace Nimbus.Contracts.Mappers;

/// <summary>Entity to DTO mapping for the admin console lives in Contracts (see docs/architecture.md).</summary>
public static class AdminMappers
{
    public static AdminUserDto MapToAdminUserDto(this User user)
    {
        return new AdminUserDto(
            user.Id, user.Email, user.FirstName, user.Name,
            user.IsApproved, user.IsRejected, user.IsBlocked, user.RequestedAt, user.Roles);
    }

    public static LoginEventDto MapToLoginEventDto(this LoginEvent loginEvent)
    {
        return new LoginEventDto(loginEvent.OccurredAt, loginEvent.Email, loginEvent.Succeeded, loginEvent.MaskedIp, loginEvent.FailureReason?.ToString());
    }
}
