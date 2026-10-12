using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nimbus.Contracts.DTOs.Features.Auth;
using Nimbus.Contracts.DTOs.Features.Pilots;
using Nimbus.Infrastructure.Persistence;
namespace Nimbus.Api.Tests.Integration;

/// <summary>Issue #25: pilots are per-user, weight is validated server side, delete is a soft delete.</summary>
public class PilotsTests
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

    private static PilotRequestDto Req(string name = "N. Thys", decimal weight = 82m) => new(name, weight);

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

    private static async Task<PilotDto> CreateAsync(HttpClient c, PilotRequestDto req)
    {
        var res = await c.PostAsJsonAsync("/api/pilots", req);
        Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        return (await res.Content.ReadFromJsonAsync<PilotDto>())!;
    }

    [Test]
    public async Task Endpoints_RequireAuthentication()
    {
        using var anon = _factory.CreateClient();
        var id = Guid.NewGuid();
        Assert.That((await anon.GetAsync("/api/pilots")).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That((await anon.GetAsync($"/api/pilots/{id}")).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That((await anon.PostAsJsonAsync("/api/pilots", Req())).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That((await anon.PutAsJsonAsync($"/api/pilots/{id}", Req())).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That((await anon.DeleteAsync($"/api/pilots/{id}")).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task Crud_RoundTrip()
    {
        var (c, _) = await SignedInAsync();
        var created = await CreateAsync(c, Req("  K. Van Dyck ", 76.5m));
        Assert.That(created.Name, Is.EqualTo("K. Van Dyck"));
        Assert.That(created.WeightKg, Is.EqualTo(76.5m));

        var list = await c.GetFromJsonAsync<List<PilotDto>>("/api/pilots");
        Assert.That(list!.Select(p => p.Id), Is.EqualTo(new[] { created.Id }));

        var put = await c.PutAsJsonAsync($"/api/pilots/{created.Id}", Req("K. Van Dyck", 78m));
        Assert.That(put.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await c.GetFromJsonAsync<PilotDto>($"/api/pilots/{created.Id}"))!.WeightKg, Is.EqualTo(78m));

        Assert.That((await c.DeleteAsync($"/api/pilots/{created.Id}")).StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        Assert.That((await c.GetAsync($"/api/pilots/{created.Id}")).StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That(await c.GetFromJsonAsync<List<PilotDto>>("/api/pilots"), Is.Empty);
    }

    [Test]
    public async Task List_IsInStableOrder_ByNameThenId()
    {
        var (c, _) = await SignedInAsync();
        await CreateAsync(c, Req("Zoe", 60m));
        await CreateAsync(c, Req("anna", 65m));
        await CreateAsync(c, Req("Bart", 90m));
        await CreateAsync(c, Req("Bart", 91m));

        var first = await c.GetFromJsonAsync<List<PilotDto>>("/api/pilots");
        var second = await c.GetFromJsonAsync<List<PilotDto>>("/api/pilots");
        Assert.That(first!.Select(p => p.Name), Is.EqualTo(new[] { "anna", "Bart", "Bart", "Zoe" }));
        Assert.That(second, Is.EqualTo(first), "same order on every call");
    }

    [Test]
    public async Task AnotherUsersPilot_Is404_AndUntouched()
    {
        var (a, _) = await SignedInAsync();
        var (b, _) = await SignedInAsync();
        var mine = await CreateAsync(a, Req("Private", 70m));

        Assert.That((await b.GetAsync($"/api/pilots/{mine.Id}")).StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That((await b.PutAsJsonAsync($"/api/pilots/{mine.Id}", Req("Hacked", 99m))).StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That((await b.DeleteAsync($"/api/pilots/{mine.Id}")).StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That(await b.GetFromJsonAsync<List<PilotDto>>("/api/pilots"), Is.Empty);

        var still = await a.GetFromJsonAsync<PilotDto>($"/api/pilots/{mine.Id}");
        Assert.That(still, Is.EqualTo(mine));
    }

    [Test]
    public async Task SameName_AllowedForDifferentUsers()
    {
        var (a, _) = await SignedInAsync();
        var (b, _) = await SignedInAsync();
        await CreateAsync(a, Req("Same", 70m));
        await CreateAsync(b, Req("Same", 71m));
        Assert.That((await a.GetFromJsonAsync<List<PilotDto>>("/api/pilots"))!.Single().WeightKg, Is.EqualTo(70m));
    }

    [TestCase("N. Thys", 0)]
    [TestCase("N. Thys", -1)]
    [TestCase("N. Thys", 19.9)]
    [TestCase("N. Thys", 250.5)]
    [TestCase("N. Thys", 82.123)]
    [TestCase("", 82)]
    [TestCase("   ", 82)]
    public async Task InvalidRequest_Is422_OnCreateAndUpdate(string name, double weight)
    {
        var (c, _) = await SignedInAsync();
        var bad = Req(name, (decimal)weight);
        Assert.That((await c.PostAsJsonAsync("/api/pilots", bad)).StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity));

        var ok = await CreateAsync(c, Req());
        Assert.That((await c.PutAsJsonAsync($"/api/pilots/{ok.Id}", bad)).StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity));
        Assert.That(await c.GetFromJsonAsync<PilotDto>($"/api/pilots/{ok.Id}"), Is.EqualTo(ok), "unchanged after a rejected update");
    }

    [Test]
    public async Task Delete_IsSoftDelete_RowRetained()
    {
        var (c, user) = await SignedInAsync();
        var created = await CreateAsync(c, Req("Keep", 80m));
        await c.DeleteAsync($"/api/pilots/{created.Id}");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.That(await db.Pilots.AnyAsync(p => p.Id == created.Id), Is.False, "hidden from normal queries");
        var row = await db.Pilots.IgnoreQueryFilters().SingleAsync(p => p.Id == created.Id);
        Assert.That(row.IsDeleted, Is.True);
        Assert.That(row.OwnerId, Is.EqualTo(user.Id));
    }
}
