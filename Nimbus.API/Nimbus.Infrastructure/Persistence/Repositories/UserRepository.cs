using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Nimbus.Application.Common.Exceptions;
using Nimbus.Domain.Entities;
using Nimbus.Domain.Interfaces;
using Nimbus.Infrastructure.Identity;
namespace Nimbus.Infrastructure.Persistence.Repositories;

public class UserRepository : IUserRepository
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public UserRepository(UserManager<ApplicationUser> userManager, AppDbContext db)
    {
        _userManager = userManager;
        _db = db;
    }

    public async Task<IReadOnlyList<User>> ListAllAsync(CancellationToken ct = default)
    {
        // Exactly two queries regardless of user count: users, then all (userId, roleName) pairs.
        var appUsers = await _db.Users.AsNoTracking().OrderBy(u => u.Email).ToListAsync(ct);
        var rolePairs = await (
            from ur in _db.UserRoles.AsNoTracking()
            join r in _db.Roles.AsNoTracking() on ur.RoleId equals r.Id
            select new { ur.UserId, RoleName = r.Name! }).ToListAsync(ct);
        var rolesByUser = rolePairs
            .GroupBy(x => x.UserId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(x => x.RoleName).OrderBy(n => n).ToList());

        return appUsers
            .Select(u => MapToDomain(u, rolesByUser.GetValueOrDefault(u.Id)))
            .ToList();
    }

    public async Task<User?> GetByIdAsync(Guid id)
    {
        var appUser = await _userManager.FindByIdAsync(id.ToString());
        return appUser is null ? null : MapToDomain(appUser, (await _userManager.GetRolesAsync(appUser)).ToList());
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        var appUser = await _userManager.FindByEmailAsync(email);
        return appUser is null ? null : MapToDomain(appUser, (await _userManager.GetRolesAsync(appUser)).ToList());
    }

    public async Task UpdateAsync(User user)
    {
        var appUser = await _userManager.FindByIdAsync(user.Id.ToString());
        if (appUser is null)
        {
            throw new NotFoundException(nameof(ApplicationUser), user.Id);
        }

        appUser.VerificationEmailCooldownEnd = user.VerificationEmailCooldownEndUtc;
        appUser.VerificationEmailSendCount = user.VerificationEmailSendCount;
        appUser.VerificationEmailLocked = user.VerificationEmailLocked;
        appUser.IsApproved = user.IsApproved;
        appUser.IsRejected = user.IsRejected;

        var result = await _userManager.UpdateAsync(appUser);
        if (!result.Succeeded)
        {
            throw new ProcessingException(nameof(ApplicationUser), appUser.Id);
        }
    }

    // ── Mapping ──────────────────────────────────────────────────────────
    // ApplicationUser never leaves Infrastructure — only Domain.User crosses the boundary
    private static User MapToDomain(ApplicationUser appUser, IReadOnlyList<string>? roles = null)
    {
        return new User(
            Guid.Parse(appUser.Id),
            appUser.Email!,
            appUser.Name,
            appUser.FirstName,
            appUser.Role,
            appUser.EmailConfirmed,
            appUser.VerificationEmailCooldownEnd,
            appUser.VerificationEmailSendCount,
            appUser.VerificationEmailLocked,
            appUser.IsApproved,
            roles,
            appUser.IsRejected,
            appUser.LockoutEnd is not null && appUser.LockoutEnd > DateTimeOffset.UtcNow,
            appUser.RequestedAt);
    }
}
