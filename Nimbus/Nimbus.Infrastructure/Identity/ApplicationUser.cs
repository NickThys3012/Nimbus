using Microsoft.AspNetCore.Identity;
using Nimbus.Domain.Enums;
namespace Nimbus.Infrastructure.Identity;

public class ApplicationUser : IdentityUser
{
    // Role stored here for Identity purposes; Domain.User.Role mirrors it
    public UserRole Role { get; set; } // UserRole enum from Domain — safe to reference

    public string FirstName { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
}
