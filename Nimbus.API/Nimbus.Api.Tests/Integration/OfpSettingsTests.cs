using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nimbus.Contracts.DTOs.Features.Auth;
using Nimbus.Contracts.DTOs.Features.OfpSettings;
using Nimbus.Infrastructure.Persistence;
namespace Nimbus.Api.Tests.Integration;

/// <summary>Issue #28: per-user OFP settings, 7 kg default, validation, reset, isolation between users.</summary>
public class OfpSettingsTests
{
    private const string Url = "/api/ofp-settings";
    private static readonly OfpSettingsDto Default = new(7m, false);
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
        Assert.That((await anon.PutAsJsonAsync(Url, new OfpSettingsRequestDto(9m))).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That((await anon.DeleteAsync(Url)).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task Get_WhenUnset_Returns7KgDefault()
    {
        var (c, _) = await SignedInAsync();
        Assert.That(await c.GetFromJsonAsync<OfpSettingsDto>(Url), Is.EqualTo(Default));
    }

    [Test]
    public async Task Save_Update_Reset_RoundTrip_KeepsSingleRowAndPrimaryKey()
    {
        var (c, user) = await SignedInAsync();
        Assert.That((await c.PutAsJsonAsync(Url, new OfpSettingsRequestDto(9m))).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(await c.GetFromJsonAsync<OfpSettingsDto>(Url), Is.EqualTo(new OfpSettingsDto(9m, true)));
        var firstId = await RowIdAsync(user.Id);

        await c.PutAsJsonAsync(Url, new OfpSettingsRequestDto(0m));
        Assert.That((await c.GetFromJsonAsync<OfpSettingsDto>(Url))!.EquipmentWeightPerPassengerKg, Is.EqualTo(0m));
        Assert.That(await RowIdAsync(user.Id), Is.EqualTo(firstId), "updated in place, same primary key");

        var reset = await c.DeleteAsync(Url);
        Assert.That(await reset.Content.ReadFromJsonAsync<OfpSettingsDto>(), Is.EqualTo(Default));
        Assert.That(await RowIdAsync(user.Id), Is.Null);
        Assert.That((await c.DeleteAsync(Url)).StatusCode, Is.EqualTo(HttpStatusCode.OK), "reset is idempotent");
    }

    [Test]
    public async Task UsersCannotSeeChangeOrResetEachOthersSettings()
    {
        var (a, userA) = await SignedInAsync();
        var (b, userB) = await SignedInAsync();
        await a.PutAsJsonAsync(Url, new OfpSettingsRequestDto(9m));

        Assert.That(await b.GetFromJsonAsync<OfpSettingsDto>(Url), Is.EqualTo(Default), "B does not see A's value");

        await b.PutAsJsonAsync(Url, new OfpSettingsRequestDto(20m));
        await b.DeleteAsync(Url);
        Assert.That(await a.GetFromJsonAsync<OfpSettingsDto>(Url), Is.EqualTo(new OfpSettingsDto(9m, true)), "A untouched by B's save and reset");
        Assert.That(await RowIdAsync(userA.Id), Is.Not.Null);
        Assert.That(await RowIdAsync(userB.Id), Is.Null);
    }

    [Test]
    public async Task InvalidValues_Are422_AndKeepPreviousValue()
    {
        var (c, _) = await SignedInAsync();
        await c.PutAsJsonAsync(Url, new OfpSettingsRequestDto(9m));
        foreach (var bad in new[] { -3m, 101m, 7.123m })
        {
            Assert.That((await c.PutAsJsonAsync(Url, new OfpSettingsRequestDto(bad))).StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity));
        }

        Assert.That((await c.GetFromJsonAsync<OfpSettingsDto>(Url))!.EquipmentWeightPerPassengerKg, Is.EqualTo(9m));
        var errors = (await (await c.PutAsJsonAsync(Url, new OfpSettingsRequestDto(-3m))).Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("errors");
        Assert.That(errors.EnumerateObject().Select(p => p.Name).ToArray(), Is.EqualTo(new[] { "Settings.EquipmentWeightPerPassengerKg" }));
    }

    private async Task<Guid?> RowIdAsync(Guid ownerId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return (await db.OfpSettings.AsNoTracking().Where(s => s.OwnerId == ownerId).Select(s => (Guid?)s.Id).ToListAsync()).SingleOrDefault();
    }
}
