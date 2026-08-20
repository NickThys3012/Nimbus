using Microsoft.AspNetCore.Identity;
using Nimbus.Application.Common.Exceptions;
using Nimbus.Domain.Enums;
using Nimbus.Domain.Interfaces;
using Nimbus.Infrastructure.Identity;
namespace Nimbus.Infrastructure.Services;

public class IdentityService:IIdentityService
{
    private readonly UserManager<ApplicationUser> _userManager;

    public IdentityService(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task<string> RegisterAsync(string email, string password, string firstName, string lastName)
    {
        var user= new ApplicationUser
        {
            UserName=email,
            Email=email,
            FirstName=firstName,
            Name=lastName
        };
        var result = await _userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            throw new ProcessingException(nameof(ApplicationUser), user.Id);
        }
        await _userManager.AddToRoleAsync(user, nameof(UserRole.Pilot));
        return user.Id;
    }
}
