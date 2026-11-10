using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Nimbus.Contracts.DTOs.Features.Auth;
using Nimbus.Contracts.DTOs.Features.Solar;
namespace Nimbus.Api.Tests.Integration;

/// <summary>Issue #49: the sun-times endpoint requires sign-in, calculates offline and validates its input.</summary>
public class SolarTests
{
    private const string Url = "/api/solar/sun-times";
    private NimbusApiFactory _factory = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _factory = new NimbusApiFactory();
        await _factory.InitializeAsync();
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown() => await _factory.DisposeAsync();

    private async Task<HttpClient> SignedInAsync()
    {
        var user = await _factory.CreateUserAsync();
        var client = _factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/authentication/login", new LoginRequestDto(user.Email, user.Password));
        res.EnsureSuccessStatusCode();
        var token = (await res.Content.ReadFromJsonAsync<LoginResponseDto>())!.AccessToken;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    [Test]
    public async Task RequiresAuthentication()
    {
        using var anon = _factory.CreateClient();
        Assert.That((await anon.GetAsync($"{Url}?latitude=51&longitude=4&date=2025-08-08")).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task ReturnsSunriseAndSunset_InUtcAndInTheRequestedZone()
    {
        var client = await SignedInAsync();

        var dto = await client.GetFromJsonAsync<SunTimesDto>($"{Url}?latitude=50.8503&longitude=4.3517&date=2025-08-08&timeZone=Europe/Brussels");

        Assert.That(dto!.Condition, Is.EqualTo("normal"));
        Assert.That(dto.SunriseUtc, Is.EqualTo(new DateTime(2025, 8, 8, 4, 19, 0, DateTimeKind.Utc)).Within(TimeSpan.FromMinutes(1)));
        Assert.That(dto.Sunrise, Does.Match("^06:(18|19|20)$"));
        Assert.That(dto.TimeZone, Is.EqualTo("Europe/Brussels"));
    }

    [Test]
    public async Task AnUnknownZone_ABadDate_OrBadCoordinates_AreRejected()
    {
        var client = await SignedInAsync();

        Assert.That((await client.GetAsync($"{Url}?latitude=51&longitude=4&date=2025-08-08&timeZone=Mars/Olympus")).StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity));
        Assert.That((await client.GetAsync($"{Url}?latitude=95&longitude=4&date=2025-08-08")).StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity));
        Assert.That((await client.GetAsync($"{Url}?latitude=51&longitude=4&date=nonsense")).IsSuccessStatusCode, Is.False);
    }
}
