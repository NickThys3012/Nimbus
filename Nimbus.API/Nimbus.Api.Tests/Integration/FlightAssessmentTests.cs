using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Nimbus.Contracts.DTOs.Features.Auth;
using Nimbus.Contracts.DTOs.Features.FlightAssessment;
using Nimbus.Contracts.DTOs.Features.GoNoGoThresholds;
namespace Nimbus.Api.Tests.Integration;

/// <summary>Issue #39: the assessment endpoint requires sign-in, validates input and uses only the caller's thresholds.</summary>
public class FlightAssessmentTests
{
    private const string Url = "/api/flight-assessment";
    private static readonly FlightAssessmentRequestDto Flight = new(100m, 50m, 20m, 30m, 80m, [70m, 60m], 500m, 12m, 8m, 100m);
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
        Assert.That((await anon.PostAsJsonAsync(Url, Flight)).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task ReturnsSingleAssessment()
    {
        var c = await SignedInAsync();
        var res = await c.PostAsJsonAsync(Url, Flight);
        res.EnsureSuccessStatusCode();
        Assert.That(await res.Content.ReadFromJsonAsync<FlightAssessmentDto>(), Is.EqualTo(new FlightAssessmentDto(410m, 130m, 90m, true, "yellow")));
    }

    [Test]
    public async Task RejectsNegativeWeight()
    {
        var c = await SignedInAsync();
        Assert.That((await c.PostAsJsonAsync(Url, Flight with { PilotWeightKg = -5m })).StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity));
    }

    [Test]
    public async Task UserBsThresholdsNeverInfluenceUserAsVerdict()
    {
        var a = await SignedInAsync();
        var b = await SignedInAsync();
        (await b.PutAsJsonAsync("/api/go-no-go-thresholds", new GoNoGoThresholdsRequestDto(30m, 40m, 2m, 1m, 900m, 1000m))).EnsureSuccessStatusCode();

        var forA = await (await a.PostAsJsonAsync(Url, Flight)).Content.ReadFromJsonAsync<FlightAssessmentDto>();
        var forB = await (await b.PostAsJsonAsync(Url, Flight)).Content.ReadFromJsonAsync<FlightAssessmentDto>();
        Assert.That(forA!.GoNoGoVerdict, Is.EqualTo("yellow"));
        Assert.That(forB!.GoNoGoVerdict, Is.EqualTo("green"));
    }
}
