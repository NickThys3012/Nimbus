using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nimbus.Contracts.DTOs.Features.Auth;
using Nimbus.Contracts.DTOs.Features.Profile;
using Nimbus.Infrastructure.Persistence;
namespace Nimbus.Api.Tests.Integration;

/// <summary>Issue #29: per-user profile (operator name, PIC weight), optional values, validation.</summary>
public class ProfileTests
{
    private const string Url = "/api/profile";
    private NimbusApiFactory _factory = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _factory = new NimbusApiFactory();
        await _factory.InitializeAsync();
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown() => await _factory.DisposeAsync();

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

    [Test]
    public async Task Endpoints_RequireAuthentication()
    {
        using var anon = _factory.CreateClient();
        Assert.That((await anon.GetAsync(Url)).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That((await anon.PutAsJsonAsync(Url, new ProfileRequestDto("x", 80m))).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task Get_WhenUnset_ReturnsEmptyValues()
    {
        var (c, _) = await SignedInAsync();
        Assert.That(await c.GetFromJsonAsync<ProfileDto>(Url), Is.EqualTo(new ProfileDto(null, null)));
    }

    [Test]
    public async Task Save_Update_Clear_RoundTrip_KeepsSingleRowAndPrimaryKey()
    {
        var (c, user) = await SignedInAsync();
        Assert.That((await c.PutAsJsonAsync(Url, new ProfileRequestDto("Skyline Ballooning", 82m))).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(await c.GetFromJsonAsync<ProfileDto>(Url), Is.EqualTo(new ProfileDto("Skyline Ballooning", 82m)));
        var firstId = await RowIdAsync(user.Id);

        await c.PutAsJsonAsync(Url, new ProfileRequestDto("Skyline Ballooning", 79.5m));
        Assert.That((await c.GetFromJsonAsync<ProfileDto>(Url))!.PicWeightKg, Is.EqualTo(79.5m));
        Assert.That(await RowIdAsync(user.Id), Is.EqualTo(firstId));

        await c.PutAsJsonAsync(Url, new ProfileRequestDto(null, null));
        Assert.That(await c.GetFromJsonAsync<ProfileDto>(Url), Is.EqualTo(new ProfileDto(null, null)));
    }

    [Test]
    public async Task UsersCannotSeeOrChangeEachOthersProfile()
    {
        var (a, userA) = await SignedInAsync();
        var (b, userB) = await SignedInAsync();
        await a.PutAsJsonAsync(Url, new ProfileRequestDto("A Ops", 80m));

        Assert.That(await b.GetFromJsonAsync<ProfileDto>(Url), Is.EqualTo(new ProfileDto(null, null)), "B does not see A's values");
        await b.PutAsJsonAsync(Url, new ProfileRequestDto("B Ops", 60m));

        Assert.That(await a.GetFromJsonAsync<ProfileDto>(Url), Is.EqualTo(new ProfileDto("A Ops", 80m)), "A untouched by B's save");
        Assert.That(await b.GetFromJsonAsync<ProfileDto>(Url), Is.EqualTo(new ProfileDto("B Ops", 60m)));
        Assert.That(await RowIdAsync(userA.Id), Is.Not.EqualTo(await RowIdAsync(userB.Id)));
    }

    [Test]
    public async Task InvalidInput_Is422_AndKeepsPreviousValues()
    {
        var (c, _) = await SignedInAsync();
        await c.PutAsJsonAsync(Url, new ProfileRequestDto("Keep", 80m));
        foreach (var bad in new[]
                 {
                     new ProfileRequestDto("x", 0m), new ProfileRequestDto("x", -1m), new ProfileRequestDto("x", 501m),
                     new ProfileRequestDto("x", 80.123m), new ProfileRequestDto(new string('a', 201), 80m),
                 })
        {
            Assert.That((await c.PutAsJsonAsync(Url, bad)).StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity));
        }

        Assert.That(await c.GetFromJsonAsync<ProfileDto>(Url), Is.EqualTo(new ProfileDto("Keep", 80m)));
    }

    [Test]
    public async Task InvalidWeight_422Body_KeysErrorByProperty()
    {
        var (c, _) = await SignedInAsync();
        var response = await c.PutAsJsonAsync(Url, new ProfileRequestDto("x", 0m));
        var errors = (await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("errors");
        Assert.That(errors.EnumerateObject().Select(p => p.Name).ToArray(), Is.EqualTo(new[] { "Profile.PicWeightKg" }));
    }

    private async Task<Guid?> RowIdAsync(Guid ownerId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return (await db.UserProfiles.AsNoTracking().Where(t => t.OwnerId == ownerId).Select(t => (Guid?)t.Id).ToListAsync()).SingleOrDefault();
    }
}
