using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Nimbus.Contracts.DTOs.Features.Auth;
using Nimbus.Contracts.DTOs.Features.FlightPreparations;
using Nimbus.Domain.Entities;
using Nimbus.Domain.Enums;
using Nimbus.Infrastructure.Persistence;
namespace Nimbus.Api.Tests.Integration;

/// <summary>Issue #40: the flight list endpoint requires sign-in and never leaks another user's flights; admins see all.</summary>
public class FlightListTests
{
    private const string Url = "/api/flight-preparations";
    private NimbusApiFactory _factory = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _factory = new NimbusApiFactory();
        await _factory.InitializeAsync();
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown() => await _factory.DisposeAsync();

    private async Task<(HttpClient Client, TestUser User)> SignedInAsync(string role = "Pilot")
    {
        var user = await _factory.CreateUserAsync(role);
        var client = _factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/authentication/login", new LoginRequestDto(user.Email, user.Password));
        res.EnsureSuccessStatusCode();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await res.Content.ReadFromJsonAsync<LoginResponseDto>())!.AccessToken);
        return (client, user);
    }

    private async Task<Guid> SeedFlightAsync(Guid owner, string title, Guid? sharedWith = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var f = new FlightPreparation { OwnerId = owner, Title = title, FlightDate = new DateOnly(2026, 8, 8), FlightTime = new TimeOnly(18, 45), WindSpeedKt = 5, VisibilityKm = 10, CapeJPerKg = 0 };
        db.FlightPreparations.Add(f);
        await db.SaveChangesAsync();
        if (sharedWith is { } user)
        {
            db.FlightPreparationShares.Add(new FlightPreparationShare { FlightPreparationId = f.Id, SharedWithUserId = user });
            await db.SaveChangesAsync();
        }

        return f.Id;
    }

    private static async Task<FlightListPageDto> ListAsync(HttpClient c, string query = "")
    {
        var res = await c.GetAsync(Url + query);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<FlightListPageDto>())!;
    }

    [Test]
    public async Task RequiresAuthentication()
    {
        using var anon = _factory.CreateClient();
        Assert.That((await anon.GetAsync(Url)).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task UserAListsOwnAndSharedFlights_NeverUserBsOthers()
    {
        var (a, userA) = await SignedInAsync();
        var (_, userB) = await SignedInAsync();
        var mine = await SeedFlightAsync(userA.Id, "mine");
        var shared = await SeedFlightAsync(userB.Id, "shared-with-a", userA.Id);
        var hidden = await SeedFlightAsync(userB.Id, "hidden-from-a");

        var all = await ListAsync(a);
        var onlyShared = await ListAsync(a, "?filter=SharedWithMe");

        Assert.That(all.Items.Select(i => i.Id), Is.EquivalentTo(new[] { mine, shared }));
        Assert.That(all.Items.Select(i => i.Id), Does.Not.Contain(hidden));
        Assert.That(onlyShared.Items.Select(i => i.Id), Is.EqualTo(new[] { shared }));
        Assert.That(onlyShared.Items[0].OwnerName, Is.EqualTo("Test User"));
        Assert.That(all.Items.Single(i => i.Id == mine).GoNoGoVerdict, Is.EqualTo("green"));
    }

    [Test]
    public async Task ClientCannotChooseTheUser_ExtraQueryParametersAreIgnored()
    {
        var (a, _) = await SignedInAsync();
        var (_, userB) = await SignedInAsync();
        await SeedFlightAsync(userB.Id, "b-only");

        var page = await ListAsync(a, $"?ownerId={userB.Id}&userId={userB.Id}");
        Assert.That(page.TotalCount, Is.Zero);
    }

    [Test]
    public async Task AdminSeesAllFlights()
    {
        var (admin, _) = await SignedInAsync("Admin");
        var (_, userB) = await SignedInAsync();
        var id = await SeedFlightAsync(userB.Id, "b-flight");

        Assert.That((await ListAsync(admin, "?pageSize=100")).Items.Select(i => i.Id), Does.Contain(id));
    }

    [Test]
    public async Task RejectsInvalidPagingAndUnknownFilter()
    {
        var (a, _) = await SignedInAsync();
        Assert.That((await a.GetAsync(Url + "?pageSize=1000")).StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity));
        Assert.That((await a.GetAsync(Url + "?page=0")).StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity));
        Assert.That((await a.GetAsync(Url + "?filter=bogus")).StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }
}
