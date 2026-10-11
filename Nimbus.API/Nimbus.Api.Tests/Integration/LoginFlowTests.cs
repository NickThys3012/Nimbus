using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Nimbus.Contracts.DTOs.Features.Auth;
using Nimbus.Contracts.DTOs.Features.Auth.Register;
using Nimbus.Infrastructure.Identity;
namespace Nimbus.Api.Tests.Integration;

/// <summary>
///     End-to-end HTTP tests for the cookie-based login → me → logout lifecycle (issues #15, #17),
///     run against the real ASP.NET Core pipeline via <see cref="NimbusApiFactory" /> so the auth cookie's
///     attributes, antiforgery enforcement, Identity lockout and per-request account validation are all
///     exercised exactly as they run in production — not just unit-tested in isolation.
///
///     Each test registers its own user (unique email) directly through the real
///     <c>/register</c> endpoint, then confirms the email + approves the account via
///     <see cref="UserManager{TUser}" /> — bypassing only the "click the link in the inbox"
///     step, since sending/receiving a real email isn't practical here (Email:Enabled is
///     false for this host anyway, see <see cref="NimbusApiFactory" />).
/// </summary>
public class LoginFlowTests
{
    private const string CookieName = TestSession.AuthCookie;
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
    public async Task Login_ThenMe_ThenLogout_Succeeds()
    {
        using var client = _factory.CreateClient();
        var email = await RegisterConfirmedUserAsync(client);
        var session = await TestSession.StartAsync(client);

        var loginResponse = await session.SendAsync(client, HttpMethod.Post, "/api/authentication/login", new LoginRequestDto(email, Password));
        Assert.That(loginResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var login = await loginResponse.Content.ReadFromJsonAsync<LoginResponseDto>();
        Assert.That(login!.Email, Is.EqualTo(email));
        Assert.That(login.IsApproved, Is.True);
        Assert.That(session.IsSignedIn, Is.True, "login must set the auth cookie");

        // /me authenticates through the cookie alone and reports identity/roles/approval.
        var meResponse = await session.SendAsync(client, HttpMethod.Get, "/api/authentication/me");
        Assert.That(meResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var me = await meResponse.Content.ReadFromJsonAsync<CurrentUserDto>();
        Assert.That(me!.Email, Is.EqualTo(email));
        Assert.That(me.IsApproved, Is.True);

        // /logout clears the cookie; the SPA's next request is anonymous.
        var logoutResponse = await session.SendAsync(client, HttpMethod.Post, "/api/authentication/logout");
        Assert.That(logoutResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(session.IsSignedIn, Is.False, "logout must clear the auth cookie");
        Assert.That((await session.SendAsync(client, HttpMethod.Get, "/api/authentication/me")).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task Login_ResponseBody_NeverCarriesACredential()
    {
        using var client = _factory.CreateClient();
        var email = await RegisterConfirmedUserAsync(client);
        var session = await TestSession.StartAsync(client);

        var body = await (await session.SendAsync(client, HttpMethod.Post, "/api/authentication/login", new LoginRequestDto(email, Password))).Content.ReadAsStringAsync();

        Assert.That(body, Does.Not.Contain("token").IgnoreCase, "the session lives only in the HttpOnly cookie, never in a script-readable body");
        Assert.That(body, Does.Not.Contain(session.Cookie(CookieName)!));
    }

    [Test]
    public async Task Login_SetsAuthCookie_WithHttpOnlySecureStrictAttributes()
    {
        using var client = _factory.CreateClient();
        var email = await RegisterConfirmedUserAsync(client);
        var session = await TestSession.StartAsync(client);

        var response = await session.SendAsync(client, HttpMethod.Post, "/api/authentication/login", new LoginRequestDto(email, Password));

        var setCookieHeader = GetSetCookieHeader(response);
        Assert.That(setCookieHeader, Is.Not.Null);
        Assert.That(setCookieHeader, Does.Contain("httponly").IgnoreCase);
        Assert.That(setCookieHeader, Does.Contain("secure").IgnoreCase);
        Assert.That(setCookieHeader, Does.Contain("samesite=strict").IgnoreCase);
    }

    [Test]
    public async Task AntiforgeryCookies_HaveTheRightAttributes()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/antiforgery");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        var cookies = response.Headers.GetValues("Set-Cookie").ToList();
        var guard = cookies.Single(c => c.StartsWith("nimbus.antiforgery=", StringComparison.OrdinalIgnoreCase));
        Assert.That(guard, Does.Contain("httponly").IgnoreCase.And.Contain("samesite=strict").IgnoreCase.And.Contain("secure").IgnoreCase);

        // The request token is the one cookie the SPA must read, so it is NOT HttpOnly.
        var readable = cookies.Single(c => c.StartsWith("XSRF-TOKEN=", StringComparison.OrdinalIgnoreCase));
        Assert.That(readable, Does.Not.Contain("httponly").IgnoreCase);
        Assert.That(readable, Does.Contain("samesite=strict").IgnoreCase.And.Contain("secure").IgnoreCase);
    }

    // ── Antiforgery enforcement ─────────────────────────────────────
    [Test]
    public async Task StateChangingRequests_WithoutAntiforgeryToken_AreRejected()
    {
        using var client = _factory.CreateClient();
        var email = await RegisterConfirmedUserAsync(client);

        // Anonymous endpoints: no token at all.
        Assert.That((await client.PostAsJsonAsync("/api/authentication/login", new LoginRequestDto(email, Password))).StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That((await client.PostAsJsonAsync("/api/authentication/register", new RegisterRequestDto($"x-{Guid.NewGuid():N}@example.test", Password, "Jane", "Doe"))).StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        // Signed in, but the XSRF header is withheld (what a cross-site form post looks like: cookies ride along, the header cannot).
        var session = await SignInAsync(client, email);
        using var forged = new HttpRequestMessage(HttpMethod.Post, "/api/authentication/logout");
        forged.Headers.Add("Cookie", string.Join("; ", new[] { CookieName, "nimbus.antiforgery" }.Select(n => $"{n}={session.Cookie(n)}")));
        Assert.That((await client.SendAsync(forged)).StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That((await session.SendAsync(client, HttpMethod.Get, "/api/authentication/me")).StatusCode, Is.EqualTo(HttpStatusCode.OK), "the forged logout must not have signed the user out");
    }

    [Test]
    public async Task StateChangingRequest_WithAnotherSessionsToken_IsRejected()
    {
        using var client = _factory.CreateClient();
        var email = await RegisterConfirmedUserAsync(client);
        var session = await SignInAsync(client, email);
        var attacker = await TestSession.StartAsync(client);

        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/authentication/logout");
        req.Headers.Add("Cookie", $"{CookieName}={session.Cookie(CookieName)}; nimbus.antiforgery={session.Cookie("nimbus.antiforgery")}");
        req.Headers.Add(TestSession.XsrfHeader, attacker.Xsrf);
        Assert.That((await client.SendAsync(req)).StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task SafeMethods_DoNotRequireAnAntiforgeryToken()
    {
        using var client = _factory.CreateClient();
        Assert.That((await client.GetAsync("/api/authentication/me")).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized), "401 (not 400): GET is exempt");
    }

    // ── Remember me ──────────────────────────────────────────────────
    [Test]
    public async Task Login_WithoutRememberMe_SetsSessionCookie_WithNoExpiresAttribute()
    {
        using var client = _factory.CreateClient();
        var email = await RegisterConfirmedUserAsync(client);
        var session = await TestSession.StartAsync(client);

        var response = await session.SendAsync(client, HttpMethod.Post, "/api/authentication/login",
            new LoginRequestDto(email, Password, RememberMe: false));

        var setCookieHeader = GetSetCookieHeader(response);
        Assert.That(setCookieHeader, Is.Not.Null);
        Assert.That(setCookieHeader, Does.Not.Contain("expires=").IgnoreCase,
            "unchecked 'remember me' must produce a session cookie (cleared on browser close), not a persistent one");
    }

    [Test]
    public async Task Login_WithRememberMe_SetsPersistentCookie_WithExpiresAttribute()
    {
        using var client = _factory.CreateClient();
        var email = await RegisterConfirmedUserAsync(client);
        var session = await TestSession.StartAsync(client);

        var response = await session.SendAsync(client, HttpMethod.Post, "/api/authentication/login",
            new LoginRequestDto(email, Password, RememberMe: true));

        var setCookieHeader = GetSetCookieHeader(response);
        Assert.That(setCookieHeader, Is.Not.Null);
        Assert.That(setCookieHeader, Does.Contain("expires=").IgnoreCase,
            "checked 'remember me' must produce a persistent cookie surviving browser restarts");
    }

    // ── Expired / invalid sessions ──────────────────────────────────
    [Test]
    public async Task Me_WithTamperedOrGarbageCookie_ReturnsUnauthorized()
    {
        using var client = _factory.CreateClient();
        using var req = new HttpRequestMessage(HttpMethod.Get, "/api/authentication/me");
        req.Headers.Add("Cookie", $"{CookieName}=CfDJ8-this-is-not-a-valid-ticket");
        Assert.That((await client.SendAsync(req)).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task Session_StopsWorking_WhenTheAccountsSecurityStampIsRotated()
    {
        using var client = _factory.CreateClient();
        var email = await RegisterConfirmedUserAsync(client);
        var session = await SignInAsync(client, email);
        Assert.That((await session.SendAsync(client, HttpMethod.Get, "/api/authentication/me")).StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // What every admin block / reject / delete / role change does to the account.
        using (var scope = _factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            await users.UpdateSecurityStampAsync((await users.FindByEmailAsync(email))!);
        }

        // The server — not the browser — ends the session, with a 401 the SPA turns into "go to login".
        Assert.That((await session.SendAsync(client, HttpMethod.Get, "/api/authentication/me")).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task ProtectedEndpoint_Returns401WhenSignedOut_And403WhenSignedInButNotAllowed()
    {
        using var client = _factory.CreateClient();
        Assert.That((await client.GetAsync("/api/admin/users")).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized), "no redirect to a login page, ever");

        var email = await RegisterConfirmedUserAsync(client);
        var session = await SignInAsync(client, email);
        Assert.That((await session.SendAsync(client, HttpMethod.Get, "/api/admin/users")).StatusCode, Is.EqualTo(HttpStatusCode.Forbidden), "a Pilot is not an Admin: the API enforces it whatever the SPA guard did");
    }

    // ── Lockout ──────────────────────────────────────────────────────
    [Test]
    public async Task Login_AfterFiveFailedAttempts_LocksAccountEvenWithCorrectPassword()
    {
        using var client = _factory.CreateClient();
        var email = await RegisterConfirmedUserAsync(client);
        var session = await TestSession.StartAsync(client);

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var response = await session.SendAsync(client, HttpMethod.Post, "/api/authentication/login",
                new LoginRequestDto(email, "WrongPassword!123"));
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        var lockedOutResponse = await session.SendAsync(client, HttpMethod.Post, "/api/authentication/login",
            new LoginRequestDto(email, Password));
        Assert.That(lockedOutResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));

        var body = await lockedOutResponse.Content.ReadAsStringAsync();
        Assert.That(body, Does.Contain("locked"));
    }

    // ── Authorization ────────────────────────────────────────────────
    [Test]
    public async Task Me_WithoutCookie_ReturnsUnauthorized()
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

        var session = await TestSession.StartAsync(client);
        var registerResponse = await session.SendAsync(client, HttpMethod.Post, "/api/authentication/register",
            new RegisterRequestDto(email, Password, "Jane", "Doe"));
        Assert.That(registerResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        var loginResponse = await session.SendAsync(client, HttpMethod.Post, "/api/authentication/login",
            new LoginRequestDto(email, Password));
        Assert.That(loginResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    // ── Helpers ──────────────────────────────────────────────────────
    private async Task<TestSession> SignInAsync(HttpClient client, string email)
    {
        var session = await TestSession.StartAsync(client);
        var response = await session.SendAsync(client, HttpMethod.Post, "/api/authentication/login", new LoginRequestDto(email, Password));
        response.EnsureSuccessStatusCode();
        return session;
    }

    private async Task<string> RegisterConfirmedUserAsync(HttpClient client)
    {
        var email = $"user-{Guid.NewGuid():N}@example.test";

        var registerResponse = await (await TestSession.StartAsync(client)).SendAsync(client, HttpMethod.Post, "/api/authentication/register",
            new RegisterRequestDto(email, Password, "Jane", "Doe"));
        Assert.That(registerResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created), await registerResponse.Content.ReadAsStringAsync());

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

    private static string? GetSetCookieHeader(HttpResponseMessage response)
    {
        return response.Headers.TryGetValues("Set-Cookie", out var values)
            ? values.FirstOrDefault(v => v.StartsWith($"{CookieName}=", StringComparison.OrdinalIgnoreCase))
            : null;
    }
}
