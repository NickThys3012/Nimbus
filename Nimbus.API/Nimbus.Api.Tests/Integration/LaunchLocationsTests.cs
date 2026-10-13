using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nimbus.Contracts.DTOs.Features.Auth;
using Nimbus.Contracts.DTOs.Features.LaunchLocations;
using Nimbus.Infrastructure.Persistence;
namespace Nimbus.Api.Tests.Integration;

/// <summary>Issue #26: launch locations are per-user, coordinates are validated server side, delete is a soft delete.</summary>
public class LaunchLocationsTests
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

    private static LaunchLocationRequestDto Req(string name = "Dendermonde", decimal lat = 51.02m, decimal lon = 4.1m, string? icao = "EBDM") => new(name, icao, lat, lon, 8m, "Class G below 1000 ft");

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

    private static async Task<LaunchLocationDto> CreateAsync(HttpClient c, LaunchLocationRequestDto req)
    {
        var res = await c.PostAsJsonAsync("/api/launch-locations", req);
        Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        return (await res.Content.ReadFromJsonAsync<LaunchLocationDto>())!;
    }

    [Test]
    public async Task Endpoints_RequireAuthentication()
    {
        using var anon = _factory.CreateClient();
        var id = Guid.NewGuid();
        Assert.That((await anon.GetAsync("/api/launch-locations")).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That((await anon.GetAsync($"/api/launch-locations/{id}")).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That((await anon.PostAsJsonAsync("/api/launch-locations", Req())).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That((await anon.PutAsJsonAsync($"/api/launch-locations/{id}", Req())).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That((await anon.DeleteAsync($"/api/launch-locations/{id}")).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task Crud_RoundTrip()
    {
        var (c, _) = await SignedInAsync();
        var created = await CreateAsync(c, Req("  Kluisbos ", 50.8m, 3.5m, "ebkt"));
        Assert.That(created.Name, Is.EqualTo("Kluisbos"));
        Assert.That(created.IcaoCode, Is.EqualTo("EBKT"));
        Assert.That(created.Latitude, Is.EqualTo(50.8m));
        Assert.That(created.ElevationM, Is.EqualTo(8m));
        Assert.That(created.AirspaceNotes, Is.EqualTo("Class G below 1000 ft"));

        var list = await c.GetFromJsonAsync<List<LaunchLocationDto>>("/api/launch-locations");
        Assert.That(list!.Select(p => p.Id), Is.EqualTo(new[] { created.Id }));

        var put = await c.PutAsJsonAsync($"/api/launch-locations/{created.Id}", Req("Kluisbos", 50.9m, 3.5m, null));
        Assert.That(put.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await c.GetFromJsonAsync<LaunchLocationDto>($"/api/launch-locations/{created.Id}"))!.Latitude, Is.EqualTo(50.9m));
        Assert.That((await c.GetFromJsonAsync<LaunchLocationDto>($"/api/launch-locations/{created.Id}"))!.IcaoCode, Is.Null, "ICAO code can be cleared");

        Assert.That((await c.DeleteAsync($"/api/launch-locations/{created.Id}")).StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        Assert.That((await c.GetAsync($"/api/launch-locations/{created.Id}")).StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That(await c.GetFromJsonAsync<List<LaunchLocationDto>>("/api/launch-locations"), Is.Empty);
    }

    [Test]
    public async Task List_IsInStableOrder_ByNameThenId()
    {
        var (c, _) = await SignedInAsync();
        await CreateAsync(c, Req("Zoe"));
        await CreateAsync(c, Req("anna"));
        await CreateAsync(c, Req("Bart", 50m));
        await CreateAsync(c, Req("Bart", 51m));

        var first = await c.GetFromJsonAsync<List<LaunchLocationDto>>("/api/launch-locations");
        var second = await c.GetFromJsonAsync<List<LaunchLocationDto>>("/api/launch-locations");
        Assert.That(first!.Select(p => p.Name), Is.EqualTo(new[] { "anna", "Bart", "Bart", "Zoe" }));
        Assert.That(second, Is.EqualTo(first), "same order on every call");
    }

    [Test]
    public async Task AnotherUsersLaunchLocation_Is404_AndUntouched()
    {
        var (a, _) = await SignedInAsync();
        var (b, _) = await SignedInAsync();
        var mine = await CreateAsync(a, Req("Private"));

        Assert.That((await b.GetAsync($"/api/launch-locations/{mine.Id}")).StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That((await b.PutAsJsonAsync($"/api/launch-locations/{mine.Id}", Req("Hacked", 10m))).StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That((await b.DeleteAsync($"/api/launch-locations/{mine.Id}")).StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That(await b.GetFromJsonAsync<List<LaunchLocationDto>>("/api/launch-locations"), Is.Empty);

        var still = await a.GetFromJsonAsync<LaunchLocationDto>($"/api/launch-locations/{mine.Id}");
        Assert.That(still, Is.EqualTo(mine));
    }

    [Test]
    public async Task SameName_AllowedForDifferentUsers()
    {
        var (a, _) = await SignedInAsync();
        var (b, _) = await SignedInAsync();
        await CreateAsync(a, Req("Same", 50m));
        await CreateAsync(b, Req("Same", 60m));
        Assert.That((await a.GetFromJsonAsync<List<LaunchLocationDto>>("/api/launch-locations"))!.Single().Latitude, Is.EqualTo(50m));
    }

    private static IEnumerable<TestCaseData> InvalidRequests()
    {
        yield return new TestCaseData(Req(lat: 90.5m), "LaunchLocation.Latitude");
        yield return new TestCaseData(Req(lat: -90.5m), "LaunchLocation.Latitude");
        yield return new TestCaseData(Req(lon: 180.5m), "LaunchLocation.Longitude");
        yield return new TestCaseData(Req(lon: -181m), "LaunchLocation.Longitude");
        yield return new TestCaseData(Req(lat: 51.1234567m), "LaunchLocation.Latitude");
        yield return new TestCaseData(Req(icao: "EBD"), "LaunchLocation.IcaoCode");
        yield return new TestCaseData(Req(icao: "EBDMX"), "LaunchLocation.IcaoCode");
        yield return new TestCaseData(Req(icao: "EB-M"), "LaunchLocation.IcaoCode");
        yield return new TestCaseData(Req(name: ""), "LaunchLocation.Name");
        yield return new TestCaseData(Req(name: "   "), "LaunchLocation.Name");
        yield return new TestCaseData(Req() with { ElevationM = 9500m }, "LaunchLocation.ElevationM");
    }

    [TestCaseSource(nameof(InvalidRequests))]
    public async Task InvalidRequest_Is422_OnCreateAndUpdate_KeyedByProperty(LaunchLocationRequestDto bad, string key)
    {
        var (c, _) = await SignedInAsync();
        var post = await c.PostAsJsonAsync("/api/launch-locations", bad);
        Assert.That(post.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity));
        var errors = (await post.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("errors");
        Assert.That(errors.EnumerateObject().Select(p => p.Name).ToArray(), Is.EqualTo(new[] { key }));

        var ok = await CreateAsync(c, Req());
        Assert.That((await c.PutAsJsonAsync($"/api/launch-locations/{ok.Id}", bad)).StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity));
        Assert.That(await c.GetFromJsonAsync<LaunchLocationDto>($"/api/launch-locations/{ok.Id}"), Is.EqualTo(ok), "unchanged after a rejected update");
    }

    [Test]
    public async Task BoundaryCoordinates_AndMissingIcao_AreAccepted()
    {
        var (c, _) = await SignedInAsync();
        var created = await CreateAsync(c, Req(lat: -90m, lon: 180m, icao: null));
        Assert.That(created.IcaoCode, Is.Null);
        Assert.That(created.Longitude, Is.EqualTo(180m));
    }

    [Test]
    public async Task Delete_IsSoftDelete_RowRetained()
    {
        var (c, user) = await SignedInAsync();
        var created = await CreateAsync(c, Req("Keep"));
        await c.DeleteAsync($"/api/launch-locations/{created.Id}");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.That(await db.LaunchLocations.AnyAsync(p => p.Id == created.Id), Is.False, "hidden from normal queries");
        var row = await db.LaunchLocations.IgnoreQueryFilters().SingleAsync(p => p.Id == created.Id);
        Assert.That(row.IsDeleted, Is.True);
        Assert.That(row.OwnerId, Is.EqualTo(user.Id));
    }
}
