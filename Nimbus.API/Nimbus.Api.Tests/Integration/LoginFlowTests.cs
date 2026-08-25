using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Nimbus.Contracts.DTOs.Features.Auth;
using Nimbus.Contracts.DTOs.Features.Auth.Register;
using Nimbus.Infrastructure.Identity;
namespace Nimbus.Api.Tests.Integration;

/// <summary>
///     End-to-end HTTP tests for the full login → refresh → logout lifecycle (issue #15),
///     run against the real ASP.NET Core pipeline via <see cref="NimbusApiFactory" /> so JWT
///     bearer validation, refresh-cookie attributes, Identity lockout and reuse-detection are
///     all exercised exactly as they run in production — not just unit-tested in isolation.
///
///     Each test registers its own user (unique email) directly through the real
///     <c>/register</c> endpoint, then confirms the email + approves the account via
///     <see cref="UserManager{TUser}" /> — bypassing only the "click the link in the inbox"
///     step, since sending/receiving a real email isn't practical here (Email:Enabled is
///     false for this host anyway, see <see cref="NimbusApiFactory" />).
/// </summary>
public class LoginFlowTests
{
    private const string CookieName = "refreshToken";
    private const string Password = "Sup3rSecure!Pass";

    private NimbusApiFactory _factory = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _factory = new NimbusApiFactory();
        await _factory.InitializeAsync();
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        await _factory.DisposeAsync();
    }

    // ── Happy path ───────────────────────────────────────────────────
    [Test]
    public async Task Login_ThenMe_ThenRefresh_ThenLogout_Succeeds()
    {
        using var client = _factory.CreateClient();
        var email = await RegisterConfirmedUserAsync(client);

        var loginResponse = await client.PostAsJsonAsync("/api/authentication/login",
            new LoginRequestDto(email, Password));
        Assert.That(loginResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var login = await loginResponse.Content.ReadFromJsonAsync<LoginResponseDto>();
        Assert.That(login, Is.Not.Null);
        Assert.That(login!.AccessToken, Is.Not.Empty);
        Assert.That(login.Email, Is.EqualTo(email));

        var refreshCookie = ExtractCookie(loginResponse, CookieName);
        Assert.That(refreshCookie, Is.Not.Null, "login must set a refreshToken cookie");

        // /me requires the bearer token and reports identity/roles/approval.
        using var meRequest = new HttpRequestMessage(HttpMethod.Get, "/api/authentication/me");
        meRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
        var meResponse = await client.SendAsync(meRequest);
        Assert.That(meResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var me = await meResponse.Content.ReadFromJsonAsync<CurrentUserDto>();
        Assert.That(me!.Email, Is.EqualTo(email));
        Assert.That(me.IsApproved, Is.True);

        // /refresh rotates the cookie and issues a new access token.
        using var refreshRequest = new HttpRequestMessage(HttpMethod.Post, "/api/authentication/refresh");
        refreshRequest.Headers.Add("Cookie", $"{CookieName}={refreshCookie}");
        var refreshResponse = await client.SendAsync(refreshRequest);
        Assert.That(refreshResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var refreshed = await refreshResponse.Content.ReadFromJsonAsync<LoginResponseDto>();
        Assert.That(refreshed!.AccessToken, Is.Not.EqualTo(login.AccessToken));

        var rotatedCookie = ExtractCookie(refreshResponse, CookieName);
        Assert.That(rotatedCookie, Is.Not.Null.And.Not.EqualTo(refreshCookie),
            "refresh must rotate the cookie value, not reuse the old one");

        // /logout revokes the (rotated) refresh token server-side.
        using var logoutRequest = new HttpRequestMessage(HttpMethod.Post, "/api/authentication/logout");
        logoutRequest.Headers.Add("Cookie", $"{CookieName}={rotatedCookie}");
        var logoutResponse = await client.SendAsync(logoutRequest);
        Assert.That(logoutResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // Replaying the (now-revoked) rotated cookie after logout must be refused.
        using var replayAfterLogout = new HttpRequestMessage(HttpMethod.Post, "/api/authentication/refresh");
        replayAfterLogout.Headers.Add("Cookie", $"{CookieName}={rotatedCookie}");
        var replayResponse = await client.SendAsync(replayAfterLogout);
        Assert.That(replayResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task Login_SetsRefreshCookie_WithHttpOnlySecureStrictAttributes()
    {
        using var client = _factory.CreateClient();
        var email = await RegisterConfirmedUserAsync(client);

        var response = await client.PostAsJsonAsync("/api/authentication/login",
            new LoginRequestDto(email, Password));

        var setCookieHeader = response.Headers.TryGetValues("Set-Cookie", out var values)
            ? values.FirstOrDefault(v => v.StartsWith($"{CookieName}=", StringComparison.OrdinalIgnoreCase))
            : null;

        Assert.That(setCookieHeader, Is.Not.Null);
        Assert.That(setCookieHeader, Does.Contain("httponly").IgnoreCase);
        Assert.That(setCookieHeader, Does.Contain("secure").IgnoreCase);
        Assert.That(setCookieHeader, Does.Contain("samesite=strict").IgnoreCase);
    }

    // ── Refresh-token reuse detection ───────────────────────────────
    [Test]
    public async Task Refresh_ReplayingRotatedOutCookie_RevokesWholeFamily()
    {
        using var client = _factory.CreateClient();
        var email = await RegisterConfirmedUserAsync(client);

        var loginResponse = await client.PostAsJsonAsync("/api/authentication/login",
            new LoginRequestDto(email, Password));
        var originalCookie = ExtractCookie(loginResponse, CookieName)!;

        // First (legitimate) rotation.
        using var firstRefresh = new HttpRequestMessage(HttpMethod.Post, "/api/authentication/refresh");
        firstRefresh.Headers.Add("Cookie", $"{CookieName}={originalCookie}");
        var firstRefreshResponse = await client.SendAsync(firstRefresh);
        Assert.That(firstRefreshResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var rotatedCookie = ExtractCookie(firstRefreshResponse, CookieName)!;

        // Replay the ORIGINAL (already-rotated-out) cookie — must be refused...
        using var replayOriginal = new HttpRequestMessage(HttpMethod.Post, "/api/authentication/refresh");
        replayOriginal.Headers.Add("Cookie", $"{CookieName}={originalCookie}");
        var replayResponse = await client.SendAsync(replayOriginal);
        Assert.That(replayResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));

        // ...and, per reuse-detection, must also revoke the still-valid rotated token, so even
        // the legitimate holder of the newest cookie is now locked out and must log in again.
        using var followUpRefresh = new HttpRequestMessage(HttpMethod.Post, "/api/authentication/refresh");
        followUpRefresh.Headers.Add("Cookie", $"{CookieName}={rotatedCookie}");
        var followUpResponse = await client.SendAsync(followUpRefresh);
        Assert.That(followUpResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task Refresh_WithoutCookie_ReturnsUnauthorized()
    {
        using var client = _factory.CreateClient();
        var response = await client.PostAsync("/api/authentication/refresh", null);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    // ── Lockout ──────────────────────────────────────────────────────
    [Test]
    public async Task Login_AfterFiveFailedAttempts_LocksAccountEvenWithCorrectPassword()
    {
        using var client = _factory.CreateClient();
        var email = await RegisterConfirmedUserAsync(client);

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var response = await client.PostAsJsonAsync("/api/authentication/login",
                new LoginRequestDto(email, "WrongPassword!123"));
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        var lockedOutResponse = await client.PostAsJsonAsync("/api/authentication/login",
            new LoginRequestDto(email, Password));
        Assert.That(lockedOutResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));

        var body = await lockedOutResponse.Content.ReadAsStringAsync();
        Assert.That(body, Does.Contain("locked"));
    }

    // ── Authorization ────────────────────────────────────────────────
    [Test]
    public async Task Me_WithoutBearerToken_ReturnsUnauthorized()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/authentication/me");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task Login_WithUnconfirmedEmail_IsRejected()
    {
        using var client = _factory.CreateClient();
        var email = $"unconfirmed-{Guid.NewGuid():N}@example.test";

        var registerResponse = await client.PostAsJsonAsync("/api/authentication/register",
            new RegisterRequestDto(email, Password, "Jane", "Doe"));
        Assert.That(registerResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        var loginResponse = await client.PostAsJsonAsync("/api/authentication/login",
            new LoginRequestDto(email, Password));
        Assert.That(loginResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    // ── Helpers ──────────────────────────────────────────────────────
    private async Task<string> RegisterConfirmedUserAsync(HttpClient client)
    {
        var email = $"user-{Guid.NewGuid():N}@example.test";

        var registerResponse = await client.PostAsJsonAsync("/api/authentication/register",
            new RegisterRequestDto(email, Password, "Jane", "Doe"));
        Assert.That(registerResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);
        Assert.That(user, Is.Not.Null, "register() must have created the user");

        // Bypasses the real "click the link in the confirmation email" step — email delivery
        // isn't exercised here (Email:Enabled=false in NimbusApiFactory), so we confirm/approve
        // directly through the same UserManager the app itself uses.
        var confirmToken = await userManager.GenerateEmailConfirmationTokenAsync(user!);
        var confirmResult = await userManager.ConfirmEmailAsync(user!, confirmToken);
        Assert.That(confirmResult.Succeeded, Is.True);

        user!.IsApproved = true;
        await userManager.UpdateAsync(user);

        return email;
    }

    private static string? ExtractCookie(HttpResponseMessage response, string cookieName)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var values))
        {
            return null;
        }

        var line = values.FirstOrDefault(v => v.StartsWith($"{cookieName}=", StringComparison.OrdinalIgnoreCase));
        if (line is null)
        {
            return null;
        }

        var afterName = line[(cookieName.Length + 1)..];
        var separatorIndex = afterName.IndexOf(';');
        return separatorIndex >= 0 ? afterName[..separatorIndex] : afterName;
    }
}
