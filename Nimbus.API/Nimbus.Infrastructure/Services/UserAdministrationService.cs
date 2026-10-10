using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Nimbus.Application.Common.Exceptions;
using Nimbus.Domain.Enums;
using Nimbus.Domain.Interfaces;
using Nimbus.Infrastructure.Identity;
using Nimbus.Infrastructure.Persistence;
namespace Nimbus.Infrastructure.Services;

public class UserAdministrationService : IUserAdministrationService
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public UserAdministrationService(UserManager<ApplicationUser> userManager, AppDbContext db)
    {
        _userManager = userManager;
        _db = db;
    }

    public async Task<int> CountUsersInRoleAsync(string role, CancellationToken ct = default)
    {
        return (await _userManager.GetUsersInRoleAsync(role)).Count;
    }

    public async Task SetRoleAsync(Guid userId, string role, CancellationToken ct = default)
    {
        var user = await FindAsync(userId);
        var current = await _userManager.GetRolesAsync(user);
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
    }

    public async Task SetBlockedAsync(Guid userId, bool blocked, CancellationToken ct = default)
    {
        var user = await FindAsync(userId);
        if (blocked)
        {
            Ensure(await _userManager.SetLockoutEnabledAsync(user, true), user);
            Ensure(await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue), user);
            await RevokeSessionsAsync(userId, ct);
        }
        else
        {
            Ensure(await _userManager.SetLockoutEndDateAsync(user, null), user);
            Ensure(await _userManager.ResetAccessFailedCountAsync(user), user);
        }
    }

    public async Task RevokeSessionsAsync(Guid userId, CancellationToken ct = default)
    {
        var id = userId.ToString();
        await _db.RefreshTokens
            .Where(t => t.UserId == id && !t.IsRevoked)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.IsRevoked, true), ct);
    }

    /// <summary>
    ///     Hard-deletes the account and its refresh tokens (no orphaned sessions). Login-audit
    ///     rows and the email send audit are intentionally kept: they hold no foreign key to
    ///     the user, only a masked IP and an email snapshot, and exist for security review.
    /// </summary>
    public async Task DeleteAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await FindAsync(userId);
        await _db.RefreshTokens.Where(t => t.UserId == user.Id).ExecuteDeleteAsync(ct);
        Ensure(await _userManager.DeleteAsync(user), user);
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
