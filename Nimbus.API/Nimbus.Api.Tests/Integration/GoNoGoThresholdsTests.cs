using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nimbus.Contracts.DTOs.Features.Auth;
using Nimbus.Contracts.DTOs.Features.GoNoGoThresholds;
using Nimbus.Domain.Entities;
using Nimbus.Infrastructure.Persistence;
namespace Nimbus.Api.Tests.Integration;

/// <summary>Issue #27: per-user thresholds, defaults, consistency validation, reset, global default is read-only.</summary>
public class GoNoGoThresholdsTests
{
    private const string Url = "/api/go-no-go-thresholds";
    private static readonly GoNoGoThresholdsDto Defaults = new(10m, 15m, 5m, 3m, 300m, 500m, false);
    private static readonly GoNoGoThresholdsRequestDto Custom = new(8m, 12m, 6m, 4m, 250m, 400m);
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
        Assert.That((await anon.PutAsJsonAsync(Url, Custom)).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That((await anon.DeleteAsync(Url)).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task Get_WhenUnset_ReturnsDefaults()
    {
        var (c, _) = await SignedInAsync();
        Assert.That(await c.GetFromJsonAsync<GoNoGoThresholdsDto>(Url), Is.EqualTo(Defaults));
    }

    [Test]
    public async Task Save_Update_Reset_RoundTrip_KeepsSingleRowAndPrimaryKey()
    {
        var (c, user) = await SignedInAsync();
        var put = await c.PutAsJsonAsync(Url, Custom);
        Assert.That(put.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(await c.GetFromJsonAsync<GoNoGoThresholdsDto>(Url), Is.EqualTo(new GoNoGoThresholdsDto(8m, 12m, 6m, 4m, 250m, 400m, true)));
        var firstId = await RowIdAsync(user.Id);

        Assert.That((await c.PutAsJsonAsync(Url, Custom with { WindRedKt = 13m })).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await c.GetFromJsonAsync<GoNoGoThresholdsDto>(Url))!.WindRedKt, Is.EqualTo(13m));
        Assert.That(await RowIdAsync(user.Id), Is.EqualTo(firstId), "updated in place, same primary key");

        var reset = await c.DeleteAsync(Url);
        Assert.That(reset.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(await reset.Content.ReadFromJsonAsync<GoNoGoThresholdsDto>(), Is.EqualTo(Defaults));
        Assert.That(await RowIdAsync(user.Id), Is.Null);
        Assert.That((await c.DeleteAsync(Url)).StatusCode, Is.EqualTo(HttpStatusCode.OK), "reset is idempotent");
    }

    [Test]
    public async Task UsersCannotSeeChangeOrResetEachOthersThresholds()
    {
        var (a, userA) = await SignedInAsync();
        var (b, userB) = await SignedInAsync();
        await a.PutAsJsonAsync(Url, Custom);

        Assert.That(await b.GetFromJsonAsync<GoNoGoThresholdsDto>(Url), Is.EqualTo(Defaults), "B does not see A's values");

        await b.PutAsJsonAsync(Url, Custom with { CapeRed = 900m });
        await b.DeleteAsync(Url);
        var aNow = await a.GetFromJsonAsync<GoNoGoThresholdsDto>(Url);
        Assert.That(aNow, Is.EqualTo(new GoNoGoThresholdsDto(8m, 12m, 6m, 4m, 250m, 400m, true)), "A's row untouched by B's save and reset");
        Assert.That(await RowIdAsync(userA.Id), Is.Not.Null);
        Assert.That(await RowIdAsync(userB.Id), Is.Null);
    }

    [Test]
    public async Task GlobalDefaultRow_IsReadOnlyFallback_NeverWritable()
    {
        var (c, user) = await SignedInAsync();
        Guid globalId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var row = new GoNoGoThreshold { Id = Guid.NewGuid(), OwnerId = null, WindYellowKt = 11m, WindRedKt = 16m, VisibilityYellowKm = 6m, VisibilityRedKm = 2m, CapeYellow = 350m, CapeRed = 550m };
            db.GoNoGoThresholds.Add(row);
            await db.SaveChangesAsync();
            globalId = row.Id;
        }

        try
        {
            Assert.That(await c.GetFromJsonAsync<GoNoGoThresholdsDto>(Url), Is.EqualTo(new GoNoGoThresholdsDto(11m, 16m, 6m, 2m, 350m, 550m, false)));

            await c.PutAsJsonAsync(Url, Custom);
            await c.DeleteAsync(Url);
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var global = await db.GoNoGoThresholds.AsNoTracking().SingleAsync(t => t.Id == globalId);
            Assert.That(global.OwnerId, Is.Null);
            Assert.That(global.WindYellowKt, Is.EqualTo(11m), "global default unchanged by a user's save and reset");
            Assert.That(await db.GoNoGoThresholds.AnyAsync(t => t.OwnerId == user.Id), Is.False);
        }
        finally
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.GoNoGoThresholds.Where(t => t.Id == globalId).ExecuteDeleteAsync();
        }
    }

    [Test]
    public async Task InconsistentOrOutOfRange_Is422_AndKeepsPreviousValues()
    {
        var (c, _) = await SignedInAsync();
        await c.PutAsJsonAsync(Url, Custom);
        foreach (var bad in new[]
                 {
                     Custom with { WindRedKt = 8m }, Custom with { VisibilityRedKm = 6m }, Custom with { CapeRed = 250m },
                     Custom with { WindYellowKt = 0m }, Custom with { CapeRed = 20000m }, Custom with { WindYellowKt = 8.123m },
                 })
        {
            Assert.That((await c.PutAsJsonAsync(Url, bad)).StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity));
        }

        Assert.That((await c.GetFromJsonAsync<GoNoGoThresholdsDto>(Url))!.WindRedKt, Is.EqualTo(12m));
    }

    [Test]
    public async Task InconsistentRequest_422Body_KeysErrorByProperty()
    {
        var (c, _) = await SignedInAsync();
        var response = await c.PutAsJsonAsync(Url, Custom with { WindRedKt = 8m });
        var errors = (await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("errors");
        Assert.That(errors.EnumerateObject().Select(p => p.Name).ToArray(), Is.EqualTo(new[] { "Thresholds.WindRedKt" }));
    }

    private async Task<Guid?> RowIdAsync(Guid ownerId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return (await db.GoNoGoThresholds.AsNoTracking().Where(t => t.OwnerId == ownerId).Select(t => (Guid?)t.Id).ToListAsync()).SingleOrDefault();
    }
}
