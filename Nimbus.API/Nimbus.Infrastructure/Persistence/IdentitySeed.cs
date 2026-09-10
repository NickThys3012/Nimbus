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
        var adminEmail = "nick.thijs@hotmail.com";
        var admin = await users.FindByEmailAsync(adminEmail);

        if (admin is null)
        {
            admin = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                FirstName = "Nick",
                Name = "Thys",
                Role = UserRole.Admin,
                PhoneNumber = "0000000000",
                PhoneNumberConfirmed = true,
                EmailConfirmed = true,
                IsApproved = true,
                VerificationEmailCooldownEnd = null,
                VerificationEmailSendCount = 0,
                VerificationEmailLocked = false
            };

            var createResult = await users.CreateAsync(admin, "Admin1234!");
            if (!createResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Failed to create seeded admin user '{adminEmail}': {string.Join(", ", createResult.Errors.Select(e => e.Description))}");
            }
        }

        admin!.EmailConfirmed = true;
        admin.PhoneNumberConfirmed = true;
        admin.IsApproved = true;
        admin.VerificationEmailCooldownEnd = null;
        admin.VerificationEmailSendCount = 0;
        admin.VerificationEmailLocked = false;

        var updateResult = await users.UpdateAsync(admin);
        if (!updateResult.Succeeded)
        {
            throw new InvalidOperationException(
                $"Failed to update seeded admin user '{adminEmail}': {string.Join(", ", updateResult.Errors.Select(e => e.Description))}");
        }

        if (!await users.IsInRoleAsync(admin, "Admin"))
        {
            var roleResult = await users.AddToRoleAsync(admin, "Admin");
            if (!roleResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Failed to assign Admin role to seeded user '{adminEmail}': {string.Join(", ", roleResult.Errors.Select(e => e.Description))}");
            }
        }
    }
}
