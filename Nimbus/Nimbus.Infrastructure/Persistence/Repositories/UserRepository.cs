using Microsoft.AspNetCore.Identity;
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

    // ── Mapping ──────────────────────────────────────────────────────────
    // ApplicationUser never leaves Infrastructure — only Domain.User crosses the boundary
    private static User MapToDomain(ApplicationUser appUser)
    {
        return new User(Guid.Parse(appUser.Id), appUser.Email!, appUser.Name, appUser.FirstName, appUser.Role);
    }
}
