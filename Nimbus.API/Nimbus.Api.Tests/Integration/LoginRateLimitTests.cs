using System.Net;
using System.Net.Http.Json;
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
        // Restore the relaxed default for fixtures that run afterwards in this process.
        Environment.SetEnvironmentVariable("RateLimiting__Login__PermitLimit", "100000");
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
}
