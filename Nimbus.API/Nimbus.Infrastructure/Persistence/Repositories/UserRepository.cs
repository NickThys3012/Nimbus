using Microsoft.AspNetCore.Identity;
using Nimbus.Application.Common.Exceptions;
using Nimbus.Domain.Entities;
using Nimbus.Domain.Interfaces;
using Nimbus.Infrastructure.Identity;
namespace Nimbus.Infrastructure.Persistence.Repositories;

public class UserRepository : IUserRepository
{
    private readonly UserManager<ApplicationUser> _userManager;

    public UserRepository(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task<User?> GetByIdAsync(Guid id)
    {
        var appUser = await _userManager.FindByIdAsync(id.ToString());
        return appUser is null ? null : MapToDomain(appUser);
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        var appUser = await _userManager.FindByEmailAsync(email);
        return appUser is null ? null : MapToDomain(appUser);
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

        var result = await _userManager.UpdateAsync(appUser);
        if (!result.Succeeded)
        {
            throw new ProcessingException(nameof(ApplicationUser), appUser.Id);
        }
    }

    // ── Mapping ──────────────────────────────────────────────────────────
    // ApplicationUser never leaves Infrastructure — only Domain.User crosses the boundary
    private static User MapToDomain(ApplicationUser appUser)
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
            appUser.VerificationEmailLocked);
    }
}
