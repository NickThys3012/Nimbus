using Microsoft.Extensions.DependencyInjection;
using Nimbus.Application.Common.Exceptions;
using Nimbus.Domain.Interfaces;
namespace Nimbus.Api.Tests.Integration;

/// <summary>
///     The "at least one active Admin must remain" rule must hold under concurrency: two admins
///     removing each other at the same moment may only succeed once.
/// </summary>
public class LastAdminConcurrencyTests
{
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

    [Test]
    public async Task ConcurrentDeletes_OfTheOnlyTwoAdmins_LeaveOneAdmin()
    {
        await ReduceToAdminsAsync(2);
        var admins = await ActiveAdminIdsAsync();

        var results = await Task.WhenAll(admins.Select(id => TryAsync(svc => svc.DeleteAsync(id))));

        Assert.That(results.Count(r => r), Is.EqualTo(1), "exactly one delete may succeed");
        Assert.That((await ActiveAdminIdsAsync()).Count, Is.EqualTo(1));
    }

    [Test]
    public async Task ConcurrentDemotions_OfTheOnlyTwoAdmins_LeaveOneAdmin()
    {
        await ReduceToAdminsAsync(2);
        var admins = await ActiveAdminIdsAsync();

        var results = await Task.WhenAll(admins.Select(id => TryAsync(svc => svc.SetRoleAsync(id, "Pilot"))));

        Assert.That(results.Count(r => r), Is.EqualTo(1), "exactly one demotion may succeed");
        Assert.That((await ActiveAdminIdsAsync()).Count, Is.EqualTo(1));
    }

    [Test]
    public async Task ConcurrentBlocks_OfTheOnlyTwoAdmins_LeaveOneActiveAdmin()
    {
        await ReduceToAdminsAsync(2);
        var admins = await ActiveAdminIdsAsync();

        var results = await Task.WhenAll(admins.Select(id => TryAsync(svc => svc.SetBlockedAsync(id, true))));

        Assert.That(results.Count(r => r), Is.EqualTo(1), "exactly one block may succeed");
        Assert.That((await ActiveAdminIdsAsync()).Count, Is.EqualTo(1));
    }

    [Test]
    public async Task TheLastAdmin_CannotBeDemotedDeletedOrBlocked()
    {
        await ReduceToAdminsAsync(1);
        var only = (await ActiveAdminIdsAsync()).Single();

        Assert.ThrowsAsync<ProcessingException>(async () => await Run(svc => svc.SetRoleAsync(only, "Pilot")));
        Assert.ThrowsAsync<ProcessingException>(async () => await Run(svc => svc.DeleteAsync(only)));
        Assert.ThrowsAsync<ProcessingException>(async () => await Run(svc => svc.SetBlockedAsync(only, true)));
    }

    [Test]
    public async Task RoleReplacement_IsAtomic_NoRolelessUserAfterAFailedChange()
    {
        await ReduceToAdminsAsync(2);
        var target = (await ActiveAdminIdsAsync()).First();

        // An invalid role name makes AddToRole fail after the old role was removed; the whole
        // replacement must roll back and leave the user an Admin.
        Assert.CatchAsync(async () => await Run(svc => svc.SetRoleAsync(target, "DoesNotExist")));

        Assert.That(await ActiveAdminIdsAsync(), Does.Contain(target));
    }

    private async Task ReduceToAdminsAsync(int count)
    {
        // Start from a known set: the seeded admin plus however many we need.
        var current = await ActiveAdminIdsAsync();
        while (current.Count < count)
        {
            await _factory.CreateUserAsync("Admin");
            current = await ActiveAdminIdsAsync();
        }
        foreach (var extra in current.Skip(count))
        {
            await Run(svc => svc.DeleteAsync(extra));
        }
    }

    private async Task<List<Guid>> ActiveAdminIdsAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Nimbus.Infrastructure.Persistence.AppDbContext>();
        var now = DateTimeOffset.UtcNow;
        var ids = (from u in db.Users
            join ur in db.UserRoles on u.Id equals ur.UserId
            join r in db.Roles on ur.RoleId equals r.Id
            where r.Name == "Admin" && u.IsApproved && (u.LockoutEnd == null || u.LockoutEnd <= now)
            select u.Id).ToList();
        return await Task.FromResult(ids.Select(Guid.Parse).ToList());
    }

    private async Task Run(Func<IUserAdministrationService, Task> action)
    {
        using var scope = _factory.Services.CreateScope();
        await action(scope.ServiceProvider.GetRequiredService<IUserAdministrationService>());
    }

    private async Task<bool> TryAsync(Func<IUserAdministrationService, Task> action)
    {
        try
        {
            await Task.Run(() => Run(action));
            return true;
        }
        catch (ProcessingException)
        {
            return false;
        }
    }
}
