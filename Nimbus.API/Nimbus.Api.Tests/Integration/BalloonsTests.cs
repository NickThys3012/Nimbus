using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Nimbus.Domain.Interfaces;
using Nimbus.Contracts.DTOs.Features.Auth;
using Nimbus.Contracts.DTOs.Features.Balloons;
using Nimbus.Domain.Entities;
using Nimbus.Infrastructure.Persistence;
namespace Nimbus.Api.Tests.Integration;

/// <summary>Issue #24: balloons are per-user, registration is unique per owner, delete is a soft delete.</summary>
public class BalloonsTests
{
    private NimbusApiFactory _factory = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _factory = new NimbusApiFactory();
        await _factory.InitializeAsync();
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown() => await _factory.DisposeAsync();

    private static BalloonRequestDto Req(string reg = "OO-ABC") => new(reg, "Cameron Z-90", 2500, 100, 120m, 80m, 20m, 30m);

    private async Task<(HttpClient Client, TestUser User)> SignedInAsync()
    {
        var user = await _factory.CreateUserAsync();
        var client = _factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/authentication/login", new LoginRequestDto(user.Email, user.Password));
        res.EnsureSuccessStatusCode();
        var token = (await res.Content.ReadFromJsonAsync<LoginResponseDto>())!.AccessToken;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return (client, user);
    }

    private static async Task<BalloonDto> CreateAsync(HttpClient c, BalloonRequestDto req)
    {
        var res = await c.PostAsJsonAsync("/api/balloons", req);
        Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        return (await res.Content.ReadFromJsonAsync<BalloonDto>())!;
    }

    [Test]
    public async Task Endpoints_RequireAuthentication()
    {
        using var anon = _factory.CreateClient();
        var id = Guid.NewGuid();
        Assert.That((await anon.GetAsync("/api/balloons")).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That((await anon.GetAsync($"/api/balloons/{id}")).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That((await anon.PostAsJsonAsync("/api/balloons", Req())).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That((await anon.PutAsJsonAsync($"/api/balloons/{id}", Req())).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That((await anon.DeleteAsync($"/api/balloons/{id}")).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task Crud_RoundTrip_WithDerivedEmptyWeight()
    {
        var (c, _) = await SignedInAsync();
        var created = await CreateAsync(c, Req("oo-crud"));
        Assert.That(created.Registration, Is.EqualTo("OO-CRUD"));
        Assert.That(created.EmptyWeightKg, Is.EqualTo(250m));

        var list = await c.GetFromJsonAsync<List<BalloonDto>>("/api/balloons");
        Assert.That(list!.Select(b => b.Id), Is.EqualTo(new[] { created.Id }));

        var put = await c.PutAsJsonAsync($"/api/balloons/{created.Id}", Req("OO-CRUD") with { BasketWeightKg = 100m });
        Assert.That(put.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var got = await c.GetFromJsonAsync<BalloonDto>($"/api/balloons/{created.Id}");
        Assert.That(got!.EmptyWeightKg, Is.EqualTo(270m));

        Assert.That((await c.DeleteAsync($"/api/balloons/{created.Id}")).StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        Assert.That((await c.GetAsync($"/api/balloons/{created.Id}")).StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That(await c.GetFromJsonAsync<List<BalloonDto>>("/api/balloons"), Is.Empty);
    }

    [Test]
    public async Task AnotherUsersBalloon_Is404_AndUntouched()
    {
        var (a, _) = await SignedInAsync();
        var (b, _) = await SignedInAsync();
        var mine = await CreateAsync(a, Req("OO-PRIV"));

        Assert.That((await b.GetAsync($"/api/balloons/{mine.Id}")).StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That((await b.PutAsJsonAsync($"/api/balloons/{mine.Id}", Req("OO-HACK"))).StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That((await b.DeleteAsync($"/api/balloons/{mine.Id}")).StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That(await b.GetFromJsonAsync<List<BalloonDto>>("/api/balloons"), Is.Empty);

        var still = await a.GetFromJsonAsync<BalloonDto>($"/api/balloons/{mine.Id}");
        Assert.That(still!.Registration, Is.EqualTo("OO-PRIV"));
    }

    [Test]
    public async Task DuplicateRegistration_SameOwner_Is409_ButOtherOwnerAndAfterDeleteAllowed()
    {
        var (a, _) = await SignedInAsync();
        var (b, _) = await SignedInAsync();
        var first = await CreateAsync(a, Req("OO-DUP"));

        Assert.That((await a.PostAsJsonAsync("/api/balloons", Req("oo-dup"))).StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        await CreateAsync(b, Req("OO-DUP"));

        await a.DeleteAsync($"/api/balloons/{first.Id}");
        await CreateAsync(a, Req("OO-DUP"));
    }

    [Test]
    public async Task Usage_EndpointIsOwnerScoped_AndEmptyUntilFlightsExist()
    {
        var (a, _) = await SignedInAsync();
        var (b, _) = await SignedInAsync();
        var mine = await CreateAsync(a, Req("OO-USE"));

        var usage = await a.GetFromJsonAsync<BalloonUsageDto>($"/api/balloons/{mine.Id}/usage");
        Assert.That(usage!.FlightCount, Is.Zero);
        Assert.That(usage.RecentFlights, Is.Empty);
        Assert.That((await b.GetAsync($"/api/balloons/{mine.Id}/usage")).StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That((await a.GetAsync($"/api/balloons/{Guid.NewGuid()}/usage")).StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        using var anon = _factory.CreateClient();
        Assert.That((await anon.GetAsync($"/api/balloons/{mine.Id}/usage")).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task Delete_WhenFlightsUseTheBalloon_Is409_AndBalloonRetained()
    {
        var (c, user) = await SignedInAsync();
        var created = await CreateAsync(c, Req("OO-BLK"));

        using var blocking = _factory.WithWebHostBuilder(b => b.ConfigureServices(s =>
        {
            s.RemoveAll<IBalloonUsageReader>();
            s.AddSingleton<IBalloonUsageReader>(new StubUsage(user.Id));
        }));
        var client = blocking.CreateClient();
        var login = await client.PostAsJsonAsync("/api/authentication/login", new LoginRequestDto(user.Email, user.Password));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await login.Content.ReadFromJsonAsync<LoginResponseDto>())!.AccessToken);

        var usage = await client.GetFromJsonAsync<BalloonUsageDto>($"/api/balloons/{created.Id}/usage");
        Assert.That(usage!.FlightCount, Is.EqualTo(9));
        Assert.That(usage.RecentFlights[0].Label, Is.EqualTo("VR 08 · Weelde"));

        Assert.That((await client.DeleteAsync($"/api/balloons/{created.Id}")).StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        Assert.That((await c.GetAsync($"/api/balloons/{created.Id}")).StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    private sealed class StubUsage(Guid expectedOwner) : IBalloonUsageReader
    {
        public Task<BalloonUsage> GetUsageAsync(Guid ownerId, Guid balloonId, CancellationToken ct = default)
            => Task.FromResult(ownerId == expectedOwner
                ? new BalloonUsage(9, [new BalloonFlightRef("VR 08 · Weelde", new DateOnly(2026, 10, 8))])
                : BalloonUsage.None);
    }

    [Test]
    public async Task TypeIsOptional_OnlyRegistrationRequired()
    {
        var (c, _) = await SignedInAsync();
        var res = await c.PostAsJsonAsync("/api/balloons", Req("OO-NOTYPE") with { Type = null });
        Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        Assert.That((await res.Content.ReadFromJsonAsync<BalloonDto>())!.Type, Is.EqualTo(""));
    }

    [Test]
    public async Task Weights_MoreThanTwoDecimals_Is422_AndTwoDecimalsRoundTrip()
    {
        var (c, _) = await SignedInAsync();
        Assert.That((await c.PostAsJsonAsync("/api/balloons", Req("OO-DEC") with { BasketWeightKg = 1.234m })).StatusCode,
            Is.EqualTo(HttpStatusCode.UnprocessableEntity));

        var created = await CreateAsync(c, Req("OO-DEC") with { BasketWeightKg = 1.25m });
        Assert.That(created.BasketWeightKg, Is.EqualTo(1.25m));
        var got = await c.GetFromJsonAsync<BalloonDto>($"/api/balloons/{created.Id}");
        Assert.That(got, Is.EqualTo(created), "what POST returned is exactly what is stored");

        Assert.That((await c.PutAsJsonAsync($"/api/balloons/{created.Id}", Req("OO-DEC") with { BurnerWeightKg = 2.999m })).StatusCode,
            Is.EqualTo(HttpStatusCode.UnprocessableEntity));
    }

    [Test]
    public async Task InvalidRequest_Is422()
    {
        var (c, _) = await SignedInAsync();
        var res = await c.PostAsJsonAsync("/api/balloons", Req("") with { EnvelopeVolumeM3 = 0 });
        Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity));
    }

    [Test]
    public async Task Delete_IsSoftDelete_RowRetainedAndResolvableForFlights()
    {
        var (c, user) = await SignedInAsync();
        var created = await CreateAsync(c, Req("OO-KEEP"));
        await c.DeleteAsync($"/api/balloons/{created.Id}");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.That(await db.Balloons.AnyAsync(b => b.Id == created.Id), Is.False, "hidden from normal queries");
        var row = await db.Balloons.IgnoreQueryFilters().SingleAsync(b => b.Id == created.Id);
        Assert.That(row.IsDeleted, Is.True);
        Assert.That(row.OwnerId, Is.EqualTo(user.Id));
        Assert.That(row.Registration, Is.EqualTo("OO-KEEP"));
        Assert.That(row.EmptyWeightKg, Is.EqualTo(250m));
    }
}
