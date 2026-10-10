namespace Nimbus.Domain.Interfaces;

/// <summary>Admin-only identity operations (roles, lockout, session revocation, deletion).</summary>
public interface IUserAdministrationService
{
    Task<int> CountUsersInRoleAsync(string role, CancellationToken ct = default);

    /// <summary>Replaces the user's roles with exactly <paramref name="role" />.</summary>
    Task SetRoleAsync(Guid userId, string role, CancellationToken ct = default);

    /// <summary>Blocks (indefinite lockout) or unblocks the user. Blocking also revokes sessions.</summary>
    Task SetBlockedAsync(Guid userId, bool blocked, CancellationToken ct = default);

    Task RevokeSessionsAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Permanently deletes the user and their refresh tokens.</summary>
    Task DeleteAsync(Guid userId, CancellationToken ct = default);
}
