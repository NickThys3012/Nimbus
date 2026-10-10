using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Nimbus.Application.Common.Exceptions;
using Nimbus.Domain.Enums;
using Nimbus.Domain.Interfaces;
using Nimbus.Infrastructure.Identity;
using Nimbus.Infrastructure.Persistence;
namespace Nimbus.Infrastructure.Services;

/// <summary>
///     Admin-only identity mutations. Every mutation runs in one transaction, and mutations that
///     can remove an active Admin first take a database application lock so the "at least one
///     active Admin must remain" check and the change are atomic across concurrent requests.
///     Every mutation also rotates the security stamp, invalidating the user's outstanding
///     access tokens (see <see cref="AccessTokenValidator" />).
/// </summary>
public class UserAdministrationService : IUserAdministrationService
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public UserAdministrationService(UserManager<ApplicationUser> userManager, AppDbContext db)
    {
        _userManager = userManager;
        _db = db;
    }

    public Task SetRoleAsync(Guid userId, string role, CancellationToken ct = default)
    {
        return InTransactionAsync(async () =>
        {
            var user = await FindAsync(userId);
            var current = await _userManager.GetRolesAsync(user);
            if (current.Contains(UserRole.Admin.ToString()) && role != nameof(UserRole.Admin))
            {
                await EnsureAnotherActiveAdminAsync(user, ct);
            }

            var toRemove = current.Where(r => !string.Equals(r, role, StringComparison.Ordinal)).ToList();
            if (toRemove.Count > 0)
            {
                Ensure(await _userManager.RemoveFromRolesAsync(user, toRemove), user);
            }
            if (!current.Contains(role))
            {
                Ensure(await _userManager.AddToRoleAsync(user, role), user);
            }

            user.Role = Enum.Parse<UserRole>(role);
            Ensure(await _userManager.UpdateAsync(user), user);
            Ensure(await _userManager.UpdateSecurityStampAsync(user), user);
        }, ct);
    }

    public Task SetBlockedAsync(Guid userId, bool blocked, CancellationToken ct = default)
    {
        return InTransactionAsync(async () =>
        {
            var user = await FindAsync(userId);
            if (blocked)
            {
                if (await _userManager.IsInRoleAsync(user, nameof(UserRole.Admin)))
                {
                    await EnsureAnotherActiveAdminAsync(user, ct);
                }
                Ensure(await _userManager.SetLockoutEnabledAsync(user, true), user);
                Ensure(await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue), user);
                Ensure(await _userManager.UpdateSecurityStampAsync(user), user);
                await RevokeSessionsAsync(userId, ct);
            }
            else
            {
                Ensure(await _userManager.SetLockoutEndDateAsync(user, null), user);
                Ensure(await _userManager.ResetAccessFailedCountAsync(user), user);
            }
        }, ct);
    }

    public async Task RevokeSessionsAsync(Guid userId, CancellationToken ct = default)
    {
        var id = userId.ToString();
        await _db.RefreshTokens
            .Where(t => t.UserId == id && !t.IsRevoked)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.IsRevoked, true), ct);
    }

    /// <summary>Rotates the security stamp so every access token issued so far stops validating.</summary>
    public async Task InvalidateAccessTokensAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await FindAsync(userId);
        Ensure(await _userManager.UpdateSecurityStampAsync(user), user);
    }

    /// <summary>
    ///     Hard-deletes the account and its refresh tokens (no orphaned sessions); the deleted
    ///     user's access tokens stop validating because the account no longer exists. Login-audit
    ///     rows and the email send audit are intentionally kept: they hold no foreign key to the
    ///     user, only a masked IP and an email snapshot, and exist for security review.
    /// </summary>
    public Task DeleteAsync(Guid userId, CancellationToken ct = default)
    {
        return InTransactionAsync(async () =>
        {
            var user = await FindAsync(userId);
            if (await _userManager.IsInRoleAsync(user, nameof(UserRole.Admin)))
            {
                await EnsureAnotherActiveAdminAsync(user, ct);
            }

            await _db.RefreshTokens.Where(t => t.UserId == user.Id).ExecuteDeleteAsync(ct);
            Ensure(await _userManager.DeleteAsync(user), user);
        }, ct);
    }

    // Retries (EnableRetryOnFailure) require the whole unit of work to run inside the execution
    // strategy; any exception, including a failed IdentityResult, rolls the transaction back.
    private async Task InTransactionAsync(Func<Task> work, CancellationToken ct)
    {
        var strategy = _db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            _db.ChangeTracker.Clear();
            await using var tx = await _db.Database.BeginTransactionAsync(ct);
            // Serialises every mutation that may reduce the set of active Admins or revoke sessions;
            // refresh-token rotation takes the same lock Shared (see AppLocks).
            await AppLocks.AcquireAsync(_db, AppLocks.AdminSet, true, ct);
            await work();
            await tx.CommitAsync(ct);
        });
    }

    /// <summary>
    ///     Throws unless an active Admin (in role Admin, approved, not locked out) other than
    ///     <paramref name="target" /> exists. Must run under the admin-set lock.
    /// </summary>
    private async Task EnsureAnotherActiveAdminAsync(ApplicationUser target, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var others = await (
            from u in _db.Users
            join ur in _db.UserRoles on u.Id equals ur.UserId
            join r in _db.Roles on ur.RoleId equals r.Id
            where r.Name == nameof(UserRole.Admin)
                  && u.Id != target.Id
                  && u.IsApproved
                  && (u.LockoutEnd == null || u.LockoutEnd <= now)
            select u.Id).AnyAsync(ct);
        if (!others)
        {
            throw new ProcessingException("The last active Admin cannot be demoted, blocked or deleted.");
        }
    }

    private async Task<ApplicationUser> FindAsync(Guid userId)
    {
        return await _userManager.FindByIdAsync(userId.ToString())
               ?? throw new NotFoundException(nameof(ApplicationUser), userId);
    }

    private static void Ensure(IdentityResult result, ApplicationUser user)
    {
        if (!result.Succeeded)
        {
            throw new ProcessingException(nameof(ApplicationUser), user.Id);
        }
    }
}
