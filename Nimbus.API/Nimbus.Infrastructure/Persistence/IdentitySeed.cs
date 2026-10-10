using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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

        // The initial admin comes from configuration (Seed:AdminEmail / Seed:AdminPassword, i.e. the
        // Seed__AdminEmail / Seed__AdminPassword environment variables) so no credential lives in
        // source. Only Development falls back to a well-known local-dev account; elsewhere, without
        // configuration, no admin is created (an existing one is left as it is).
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var env = scope.ServiceProvider.GetRequiredService<IHostEnvironment>();
        var adminEmail = config["Seed:AdminEmail"];
        var adminPassword = config["Seed:AdminPassword"];
        if (env.IsDevelopment())
        {
            adminEmail ??= "admin@nimbus.local";
            adminPassword ??= "Admin1234!";
        }
        if (string.IsNullOrWhiteSpace(adminEmail) || string.IsNullOrWhiteSpace(adminPassword))
        {
            scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("IdentitySeed")
                .LogWarning("Seed:AdminEmail / Seed:AdminPassword are not configured; no admin user was seeded.");
            return;
        }

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

            var createResult = await users.CreateAsync(admin, adminPassword);
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
