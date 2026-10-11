using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Nimbus.Contracts.DTOs.Features.Auth;
namespace Nimbus.Api.Tests.Integration;

/// <summary>The login endpoint is rate limited per IP (bounds guessing and login-audit growth).</summary>
public class LoginRateLimitTests
{
    private NimbusApiFactory _factory = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _factory = new NimbusApiFactory();
        await _factory.InitializeAsync(3);
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        await _factory.DisposeAsync();
        // Restore the relaxed defaults for fixtures that run afterwards in this process.
        Environment.SetEnvironmentVariable("RateLimiting__Login__PermitLimit", "100000");
        Environment.SetEnvironmentVariable("ForwardedHeaders__KnownNetworks__0", null);
    }

    [Test]
    public async Task Login_IsRejectedWith429_AfterTheLimit()
    {
        using var client = _factory.CreateClient();
        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 5; i++)
        {
            statuses.Add((await client.PostAsJsonAsync("/api/authentication/login", new LoginRequestDto("nobody@example.test", "x"))).StatusCode);
        }

        Assert.That(statuses.Take(3), Is.All.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That(statuses.Skip(3), Is.All.EqualTo(HttpStatusCode.TooManyRequests));
    }

    private static async Task<HttpStatusCode> LoginFromAsync(HttpClient client, string forwardedFor)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/authentication/login")
        {
            Content = JsonContent.Create(new LoginRequestDto("nobody@example.test", "x"))
        };
        req.Headers.Add("X-Forwarded-For", forwardedFor);
        return (await client.SendAsync(req)).StatusCode;
    }

    [Test]
    public async Task TrustedProxy_DifferentForwardedClients_GetSeparateLimiterPartitions_AndMaskedIpIsTheClients()
    {
        using var client = _factory.CreateClient();
        for (var i = 0; i < 3; i++)
        {
            Assert.That(await LoginFromAsync(client, "84.198.12.9"), Is.EqualTo(HttpStatusCode.Unauthorized));
        }
        Assert.That(await LoginFromAsync(client, "84.198.12.9"), Is.EqualTo(HttpStatusCode.TooManyRequests), "client A is throttled");
        Assert.That(await LoginFromAsync(client, "45.227.1.2"), Is.EqualTo(HttpStatusCode.Unauthorized), "client B is not");

        // The audit row carries the forwarded client, masked, not the proxy address.
        // (Failed attempts for unknown accounts are recorded too.)
        using var scope = _factory.Services.CreateScope();
        var ips = scope.ServiceProvider.GetRequiredService<Nimbus.Infrastructure.Persistence.AppDbContext>()
            .LoginEvents.Select(e => e.MaskedIp).ToList();
        Assert.That(ips, Does.Contain("84.198.x.x"));
        Assert.That(ips, Does.Contain("45.227.x.x"));
    }
}

/// <summary>An untrusted peer must not be able to choose its own rate-limit partition or logged IP.</summary>
public class UntrustedProxyForwardedHeadersTests
{
    private NimbusApiFactory _factory = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _factory = new NimbusApiFactory();
        // Loopback (the test peer) is NOT in the trusted set.
        await _factory.InitializeAsync(2, "203.0.113.0/24");
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        await _factory.DisposeAsync();
        Environment.SetEnvironmentVariable("RateLimiting__Login__PermitLimit", "100000");
        Environment.SetEnvironmentVariable("ForwardedHeaders__KnownNetworks__0", null);
    }

    [Test]
    public async Task SpoofedForwardedFor_IsIgnored_SoClientsShareThePeersPartition()
    {
        using var client = _factory.CreateClient();
        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 4; i++)
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "/api/authentication/login")
            {
                Content = JsonContent.Create(new LoginRequestDto("nobody@example.test", "x"))
            };
            req.Headers.Add("X-Forwarded-For", $"198.51.100.{i + 1}");
            statuses.Add((await client.SendAsync(req)).StatusCode);
        }

        Assert.That(statuses, Is.EqualTo(new[]
        {
            HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized, HttpStatusCode.TooManyRequests, HttpStatusCode.TooManyRequests
        }));
    }
}
