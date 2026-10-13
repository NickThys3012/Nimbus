using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nimbus.Contracts.DTOs.Features.Auth;
using Nimbus.Infrastructure.Persistence;
namespace Nimbus.Api.Tests.Integration;

/// <summary>
///     Issue #30 (NB-026): one ownership suite run over every reference entity (balloons, pilots, launch
///     locations) through the real HTTP pipeline and database. Complements the service-level contract in
///     Nimbus.Application.Tests (OwnedServiceContract).
/// </summary>
public class ReferenceDataOwnershipTests
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

    public static IEnumerable<TestCaseData> Entities()
    {
        yield return new TestCaseData("/api/balloons", (Func<string, JsonObject>)(tag => new JsonObject
        {
            ["registration"] = $"OO-{tag}", ["type"] = "Cameron Z-90", ["envelopeVolumeM3"] = 2500, ["maxEnvelopeTemperatureC"] = 100,
            ["envelopeWeightKg"] = 120, ["basketWeightKg"] = 80, ["burnerWeightKg"] = 20, ["cylindersWeightKg"] = 30
        }), "registration").SetName("Balloons");
        yield return new TestCaseData("/api/pilots", (Func<string, JsonObject>)(tag => new JsonObject
        {
            ["name"] = $"Pilot {tag}", ["weightKg"] = 82
        }), "name").SetName("Pilots");
        yield return new TestCaseData("/api/launch-locations", (Func<string, JsonObject>)(tag => new JsonObject
        {
            ["name"] = $"Field {tag}", ["icaoCode"] = null, ["latitude"] = 51.02, ["longitude"] = 4.1, ["elevationM"] = 8, ["airspaceNotes"] = null
        }), "name").SetName("LaunchLocations");
    }

    private async Task<(HttpClient Client, TestUser User)> SignedInAsync(string role = "Pilot")
    {
        var user = await _factory.CreateUserAsync(role);
        var client = _factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/authentication/login", new LoginRequestDto(user.Email, user.Password));
        res.EnsureSuccessStatusCode();
        var token = (await res.Content.ReadFromJsonAsync<LoginResponseDto>())!.AccessToken;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return (client, user);
    }

    private static string Tag() => Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

    private static async Task<JsonObject> CreateAsync(HttpClient c, string url, JsonObject body)
    {
        var res = await c.PostAsJsonAsync(url, body);
        Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        return (await res.Content.ReadFromJsonAsync<JsonObject>())!;
    }

    private async Task<Guid> OwnerOfAsync(string url, Guid id)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return url switch
        {
            "/api/balloons" => (await db.Balloons.IgnoreQueryFilters().SingleAsync(e => e.Id == id)).OwnerId,
            "/api/pilots" => (await db.Pilots.IgnoreQueryFilters().SingleAsync(e => e.Id == id)).OwnerId,
            _ => (await db.LaunchLocations.IgnoreQueryFilters().SingleAsync(e => e.Id == id)).OwnerId
        };
    }

    [TestCaseSource(nameof(Entities))]
    public async Task NonOwner_CannotReadUpdateOrDelete_AndRowIsUntouched(string url, Func<string, JsonObject> make, string field)
    {
        var (a, _) = await SignedInAsync();
        var (b, _) = await SignedInAsync();
        var mine = await CreateAsync(a, url, make(Tag()));
        var id = mine["id"]!.GetValue<Guid>();

        Assert.That((await b.GetAsync($"{url}/{id}")).StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That((await b.PutAsJsonAsync($"{url}/{id}", make(Tag()))).StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That((await b.DeleteAsync($"{url}/{id}")).StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        var bList = await b.GetFromJsonAsync<JsonArray>(url);
        Assert.That(bList!.Select(n => n!["id"]!.GetValue<Guid>()), Does.Not.Contain(id));

        var still = await a.GetFromJsonAsync<JsonObject>($"{url}/{id}");
        Assert.That(still![field]!.ToString(), Is.EqualTo(mine[field]!.ToString()));
    }

    [TestCaseSource(nameof(Entities))]
    public async Task Create_IgnoresAttackerSuppliedOwnerId(string url, Func<string, JsonObject> make, string field)
    {
        var (a, userA) = await SignedInAsync();
        var (_, victim) = await SignedInAsync();
        var body = make(Tag());
        body["ownerId"] = victim.Id;
        body["OwnerId"] = victim.Id;
        body["userId"] = victim.Id;

        var created = await CreateAsync(a, url, body);
        var id = created["id"]!.GetValue<Guid>();

        Assert.That(await OwnerOfAsync(url, id), Is.EqualTo(userA.Id));
    }

    [TestCaseSource(nameof(Entities))]
    public async Task Update_CannotReassignOwnership_ThroughBodyOwnerId(string url, Func<string, JsonObject> make, string field)
    {
        var (a, userA) = await SignedInAsync();
        var (_, victim) = await SignedInAsync();
        var created = await CreateAsync(a, url, make(Tag()));
        var id = created["id"]!.GetValue<Guid>();

        var body = make(Tag());
        body["ownerId"] = victim.Id;
        body["OwnerId"] = victim.Id;
        var put = await a.PutAsJsonAsync($"{url}/{id}", body);
        Assert.That(put.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        Assert.That(await OwnerOfAsync(url, id), Is.EqualTo(userA.Id));
        Assert.That((await a.GetAsync($"{url}/{id}")).StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    /// <summary>
    ///     The HTTP endpoints always act as the signed-in user, admins included (handlers build
    ///     Caller(userId, false)). Cross-owner admin access exists only in the service layer, which is
    ///     asserted in Nimbus.Application.Tests; this pins the HTTP behaviour so a change is deliberate.
    /// </summary>
    [TestCaseSource(nameof(Entities))]
    public async Task Admin_OverHttp_IsScopedLikeAnyUser(string url, Func<string, JsonObject> make, string field)
    {
        var (a, _) = await SignedInAsync();
        var (admin, _) = await SignedInAsync("Admin");
        var mine = await CreateAsync(a, url, make(Tag()));
        var id = mine["id"]!.GetValue<Guid>();

        Assert.That((await admin.GetAsync($"{url}/{id}")).StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That((await admin.DeleteAsync($"{url}/{id}")).StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That((await a.GetAsync($"{url}/{id}")).StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }
}
