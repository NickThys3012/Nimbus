namespace Nimbus.Contracts.DTOs.Features.Auth;

/// <summary>
///     Reported by GET /api/authentication/me so the Angular shell can render the right
///     screen for the signed-in user (issue #15): who they are, what they're allowed to do,
///     and whether an administrator has approved their account yet.
/// </summary>
public record CurrentUserDto(
    string Id,
    string Email,
    string FirstName,
    string Name,
    IReadOnlyList<string> Roles,
    bool IsApproved);
