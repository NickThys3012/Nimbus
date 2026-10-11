using Microsoft.AspNetCore.Identity;
using Nimbus.Application.Common.Exceptions;
using Nimbus.Application.Common.Interfaces;
using Nimbus.Application.Helpers;
using Nimbus.Domain.Enums;
using Nimbus.Domain.Interfaces;
using Nimbus.Infrastructure.Identity;
namespace Nimbus.Infrastructure.Services;

public class IdentityService : IIdentityService
{
    private readonly IEmailQueue _emailQueue;
    private readonly UserManager<ApplicationUser> _userManager;

    public IdentityService(UserManager<ApplicationUser> userManager, IEmailQueue emailQueue)
    {
        _userManager = userManager;
        _emailQueue = emailQueue;
    }

    public async Task<string> RegisterAsync(string email, string password, string firstName, string lastName)
    {
        var user = new ApplicationUser
        {
            UserName = email, Email = email, FirstName = firstName, Name = lastName, Role = UserRole.Pilot, RequestedAt = DateTime.UtcNow
        };
        var result = await _userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            throw new ProcessingException(nameof(ApplicationUser), user.Id);
        }
        await _userManager.AddToRoleAsync(user, nameof(UserRole.Pilot));
        return user.Id;
    }
    public async Task<string> GenerateEmailConfrimTokenAsync(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
        {
            throw new NotFoundException(nameof(ApplicationUser), id);
        }
        var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        return token;
    }

    public async Task RequestPasswordResetAsync(string email, string webBaseUrl, CancellationToken ct = default)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user is null)
        {
            return;
        }

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        // Angular route, not an API endpoint: the SPA collects the new password and posts it back.
        var link = $"{webBaseUrl.TrimEnd('/')}/reset-password?email={Uri.EscapeDataString(email)}&token={Uri.EscapeDataString(token)}";
        await _emailQueue.EnqueueAsync(EmailHelpers.CreatePasswordReset(user.FirstName, user.Email!, link), ct);
    }

    public async Task<bool> ResetPasswordAsync(string email, string token, string newPassword, CancellationToken ct = default)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user is null)
        {
            return false;
        }

        // ResetPasswordAsync rotates the security stamp, which is what makes the token single-use
        // and also invalidates outstanding access tokens.
        var result = await _userManager.ResetPasswordAsync(user, token, newPassword);
        return result.Succeeded;
    }
}
