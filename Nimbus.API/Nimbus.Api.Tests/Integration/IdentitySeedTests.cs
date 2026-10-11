using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Nimbus.Infrastructure.Identity;
using Nimbus.Infrastructure.Persistence;
namespace Nimbus.Api.Tests.Integration;

/// <summary>The startup seed creates an admin only while no Admin exists (see IdentitySeed.SeedAdminAsync).</summary>
public class IdentitySeedTests
{
    private const string Password = "Seed-Passw0rd!x";
    private NimbusApiFactory _factory = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _factory = new NimbusApiFactory();
        await _factory.InitializeAsync();
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        await _factory.DisposeAsync();
    }

    private async Task<T> WithUsersAsync<T>(Func<UserManager<ApplicationUser>, Task<T>> action)
    {
        using var scope = _factory.Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>());
    }

    [Test]
    public async Task SeedsOnlyWhenNoAdminExists_AndNeverAddsOrPromotesAnotherAdminAfterwards()
    {
        // Existing deployment: the startup seed already made an admin. A different configured email must do nothing.
        var other = $"other-{Guid.NewGuid():N}@example.test";
        await WithUsersAsync(async um =>
        {
            await IdentitySeed.SeedAdminAsync(um, other, Password, false, NullLogger.Instance);
            return 0;
        });
        Assert.That(await WithUsersAsync(um => um.FindByEmailAsync(other)), Is.Null, "no extra admin created");

        // A normal pilot with the configured email must not be promoted either.
        var pilot = await _factory.CreateUserAsync();
        await WithUsersAsync(async um =>
        {
            await IdentitySeed.SeedAdminAsync(um, pilot.Email, Password, false, NullLogger.Instance);
            return 0;
        });
        Assert.That(await WithUsersAsync(async um => await um.IsInRoleAsync((await um.FindByEmailAsync(pilot.Email))!, "Admin")), Is.False);

        // Remove every admin: now the seed must create the configured one ...
        await WithUsersAsync(async um =>
        {
            foreach (var admin in await um.GetUsersInRoleAsync("Admin"))
            {
                await um.DeleteAsync(admin);
            }
            return 0;
        });
        var seeded = $"seeded-{Guid.NewGuid():N}@example.test";
        await WithUsersAsync(async um =>
        {
            await IdentitySeed.SeedAdminAsync(um, seeded, Password, false, NullLogger.Instance);
            return 0;
        });
        var created = await WithUsersAsync(async um =>
        {
            var u = await um.FindByEmailAsync(seeded);
            return u is not null && u.IsApproved && await um.IsInRoleAsync(u, "Admin");
        });
        Assert.That(created, Is.True);

        // ... and re-running (even with another email) is idempotent.
        await WithUsersAsync(async um =>
        {
            await IdentitySeed.SeedAdminAsync(um, seeded, Password, false, NullLogger.Instance);
            await IdentitySeed.SeedAdminAsync(um, other, Password, false, NullLogger.Instance);
            return 0;
        });
        Assert.That(await WithUsersAsync(um => um.GetUsersInRoleAsync("Admin")), Has.Count.EqualTo(1));
        Assert.That(await WithUsersAsync(um => um.FindByEmailAsync(other)), Is.Null);
    }

    [Test]
    public async Task NoAdminAndNoConfiguration_SeedsNothing_ExceptTheDevelopmentDefault()
    {
        await WithUsersAsync(async um =>
        {
            foreach (var admin in await um.GetUsersInRoleAsync("Admin"))
            {
                await um.DeleteAsync(admin);
            }
            await IdentitySeed.SeedAdminAsync(um, null, null, false, NullLogger.Instance);
            return 0;
        });
        Assert.That(await WithUsersAsync(um => um.GetUsersInRoleAsync("Admin")), Is.Empty);

        await WithUsersAsync(async um =>
        {
            await IdentitySeed.SeedAdminAsync(um, null, null, true, NullLogger.Instance);
            return 0;
        });
        Assert.That(await WithUsersAsync(um => um.FindByEmailAsync("admin@nimbus.local")), Is.Not.Null);
    }
}
