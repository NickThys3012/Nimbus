using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Nimbus.Domain.Enums;
using Nimbus.Infrastructure.Identity;
namespace Nimbus.Infrastructure.Persistence;

public static class IdentitySeed
{
    public static async Task SeedUsers(this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var users = scope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();
        var roles = scope.ServiceProvider
            .GetRequiredService<RoleManager<IdentityRole>>();

        // Create roles
        foreach (var role in Enum.GetNames<UserRole>())
        {
            if (!await roles.RoleExistsAsync(role))
            {
                await roles.CreateAsync(new IdentityRole(role));
            }
        }

        // Create admin user
        if (await users.FindByEmailAsync("nick.thijs@hotmail.com") is null)
        {
            var admin = new ApplicationUser
            {
                UserName = "nick.thijs@hotmail.com",
                Email = "nick.thijs@hotmail.com",
                FirstName = "Nick",
                Name = "Thys",
                Role = UserRole.Admin,
                PhoneNumber = "0000000000"
            };
            await users.CreateAsync(admin, "Admin1234!");
            await users.AddToRoleAsync(admin, "Admin");
        }
    }
}
