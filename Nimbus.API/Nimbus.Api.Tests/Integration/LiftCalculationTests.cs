using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Nimbus.Contracts.DTOs.Features.Auth;
using Nimbus.Contracts.DTOs.Features.LiftCalculation;
namespace Nimbus.Api.Tests.Integration;

/// <summary>Issue #37: the stateless total-lift endpoint requires sign-in, matches the manual formula and validates input.</summary>
public class LiftCalculationTests
{
    private const string Url = "/api/lift-calculation/total-lift";
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

    private static string Query(string altitude, string elevation, string ambient, string internalTemp, string volume) =>
        $"{Url}?maxAltitudeMeters={altitude}&siteElevationMeters={elevation}&ambientTemperatureC={ambient}&internalTemperatureC={internalTemp}&envelopeVolumeM3={volume}";

    [Test]
    public async Task RequiresAuthentication()
    {
        using var anon = _factory.CreateClient();
        Assert.That((await anon.GetAsync(Query("1000", "0", "15", "100", "3000"))).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task ReturnsManualFormulaResult()
    {
        var client = await SignedInAsync();
        var dto = await client.GetFromJsonAsync<TotalLiftDto>(Query("1000", "0", "15", "100", "3000"));
        Assert.That(dto!.TotalLiftKg, Is.EqualTo(817.8m));
    }

    [Test]
    public async Task InternalTemperatureAbove100_IsClamped()
    {
        var client = await SignedInAsync();
        var hot = await client.GetFromJsonAsync<TotalLiftDto>(Query("0", "0", "15", "250", "1000"));
        var max = await client.GetFromJsonAsync<TotalLiftDto>(Query("0", "0", "15", "100", "1000"));
        Assert.That(hot, Is.EqualTo(max));
    }

    [Test]
    public async Task OutOfRangeInput_IsRejected()
    {
        var client = await SignedInAsync();
        var res = await client.GetAsync(Query("1000", "0", "15", "100", "-5"));
        Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity));
    }
}
