namespace Nimbus.Domain.Interfaces;

/// <summary>Admin-only identity operations (roles, lockout, session revocation, deletion).</summary>
public interface IUserAdministrationService
{
    /// <summary>
    ///     Replaces the user's roles with exactly <paramref name="role" />, atomically. Refuses (ProcessingException)
    ///     to demote the last active Admin. Invalidates the user's access tokens.
    /// </summary>
    Task SetRoleAsync(Guid userId, string role, CancellationToken ct = default);

    /// <summary>
    ///     Blocks (indefinite lockout) or unblocks the user. Blocking revokes sessions and access tokens and refuses to
    ///     block the last active Admin.
    /// </summary>
    Task SetBlockedAsync(Guid userId, bool blocked, CancellationToken ct = default);

    Task RevokeSessionsAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Invalidates every access token issued to the user so far.</summary>
    Task InvalidateAccessTokensAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Permanently deletes the user and their refresh tokens. Refuses to delete the last active Admin.</summary>
    Task DeleteAsync(Guid userId, CancellationToken ct = default);
}
