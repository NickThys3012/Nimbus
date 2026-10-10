using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Nimbus.Contracts.DTOs.Features.Admin;
using Nimbus.Contracts.DTOs.Features.Auth;
using Nimbus.Contracts.DTOs.Features.Auth.Register;
using Nimbus.Infrastructure.Identity;
namespace Nimbus.Api.Tests.Integration;

/// <summary>Issue #19: admin console endpoints are Admin-only and approval persists.</summary>
public class AdminUsersTests
{
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

    [Test]
    public async Task Endpoints_RejectAnonymousAndPilot()
    {
        using var client = _factory.CreateClient();
        var (pilotToken, pilotId) = await RegisterAndLoginAsync(client, approved: true);

        Assert.That((await client.GetAsync("/api/admin/users")).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));

        using var list = Authed(HttpMethod.Get, "/api/admin/users", pilotToken);
        Assert.That((await client.SendAsync(list)).StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));

        using var approve = Authed(HttpMethod.Post, $"/api/admin/users/{pilotId}/approve", pilotToken);
        Assert.That((await client.SendAsync(approve)).StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task Admin_CanListAndApprove()
    {
        using var client = _factory.CreateClient();
        var (_, pendingId) = await RegisterAndLoginAsync(client, approved: false);
        var adminToken = await LoginAdminAsync(client);

        using var list = Authed(HttpMethod.Get, "/api/admin/users", adminToken);
        var users = await (await client.SendAsync(list)).Content.ReadFromJsonAsync<List<AdminUserDto>>();
        var pending = users!.Single(u => u.Id == pendingId);
        Assert.That(pending.IsApproved, Is.False);
        Assert.That(pending.Roles, Does.Contain("Pilot"));

        using var approve = Authed(HttpMethod.Post, $"/api/admin/users/{pendingId}/approve", adminToken);
        Assert.That((await client.SendAsync(approve)).StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        using var scope = _factory.Services.CreateScope();
        var user = await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByIdAsync(pendingId.ToString());
        Assert.That(user!.IsApproved, Is.True);
    }

    [Test]
    public async Task Admin_ApproveUnknownUser_Returns404()
    {
        using var client = _factory.CreateClient();
        var adminToken = await LoginAdminAsync(client);
        using var approve = Authed(HttpMethod.Post, $"/api/admin/users/{Guid.NewGuid()}/approve", adminToken);
        Assert.That((await client.SendAsync(approve)).StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    // ── authorisation on every admin endpoint ──
    [Test]
    public async Task NewEndpoints_Reject_Anonymous_And_Pilot()
    {
        using var client = _factory.CreateClient();
        var (pilotToken, pilotId) = await RegisterAndLoginAsync(client, approved: true);
        var calls = new (HttpMethod Method, string Url)[]
        {
            (HttpMethod.Post, $"/api/admin/users/{pilotId}/reject"),
            (HttpMethod.Put, $"/api/admin/users/{pilotId}/role"),
            (HttpMethod.Post, $"/api/admin/users/{pilotId}/block"),
            (HttpMethod.Post, $"/api/admin/users/{pilotId}/unblock"),
            (HttpMethod.Delete, $"/api/admin/users/{pilotId}"),
            (HttpMethod.Get, "/api/admin/login-events")
        };

        foreach (var (method, url) in calls)
        {
            using var anon = new HttpRequestMessage(method, url);
            Assert.That((await client.SendAsync(anon)).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized), $"anon {method} {url}");
            using var pilot = Authed(method, url, pilotToken);
            Assert.That((await client.SendAsync(pilot)).StatusCode, Is.EqualTo(HttpStatusCode.Forbidden), $"pilot {method} {url}");
        }
    }

    // ── reject ──
    [Test]
    public async Task Admin_CanReject_RejectedCannotBeApproved_AndSpaSeesFlag()
    {
        using var client = _factory.CreateClient();
        var (oldToken, pilotId) = await RegisterAndLoginAsync(client, approved: false, out var pilotEmail);
        var adminToken = await LoginAdminAsync(client);

        Assert.That((await Send(client, HttpMethod.Post, $"/api/admin/users/{pilotId}/reject", adminToken)).StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        Assert.That((await Send(client, HttpMethod.Get, "/api/authentication/me", oldToken)).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized), "reject invalidates outstanding tokens");
        var pilotToken = await LoginAsync(client, pilotEmail, Password);

        var listed = (await ListUsersAsync(client, adminToken)).Single(u => u.Id == pilotId);
        Assert.That(listed.IsRejected, Is.True);
        Assert.That(listed.IsApproved, Is.False);
        Assert.That(listed.RequestedAt, Is.Not.Null);

        Assert.That((await Send(client, HttpMethod.Post, $"/api/admin/users/{pilotId}/approve", adminToken)).StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        using var me = Authed(HttpMethod.Get, "/api/authentication/me", pilotToken);
        var current = await (await client.SendAsync(me)).Content.ReadFromJsonAsync<CurrentUserDto>();
        Assert.That(current!.IsRejected, Is.True);

        Assert.That((await Send(client, HttpMethod.Post, $"/api/admin/users/{Guid.NewGuid()}/reject", adminToken)).StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That((await Send(client, HttpMethod.Post, $"/api/admin/users/{await AdminIdAsync(client, adminToken)}/reject", adminToken)).StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    // ── role ──
    [Test]
    public async Task Admin_CanChangeRole_ButNotSelfDemote_AndRoleMustExist()
    {
        using var client = _factory.CreateClient();
        var (_, pilotId) = await RegisterAndLoginAsync(client, approved: true);
        var adminToken = await LoginAdminAsync(client);

        var res = await Send(client, HttpMethod.Put, $"/api/admin/users/{pilotId}/role", adminToken, new SetUserRoleRequestDto("Admin"));
        Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        Assert.That((await ListUsersAsync(client, adminToken)).Single(u => u.Id == pilotId).Roles, Is.EqualTo(new[] { "Admin" }));

        // Back to Pilot (another admin exists, so it is allowed).
        Assert.That((await Send(client, HttpMethod.Put, $"/api/admin/users/{pilotId}/role", adminToken, new SetUserRoleRequestDto("Pilot"))).StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        var selfId = await AdminIdAsync(client, adminToken);
        Assert.That((await Send(client, HttpMethod.Put, $"/api/admin/users/{selfId}/role", adminToken, new SetUserRoleRequestDto("Pilot"))).StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That((await Send(client, HttpMethod.Put, $"/api/admin/users/{pilotId}/role", adminToken, new SetUserRoleRequestDto("crew"))).StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity));
        Assert.That((await Send(client, HttpMethod.Put, $"/api/admin/users/{Guid.NewGuid()}/role", adminToken, new SetUserRoleRequestDto("Pilot"))).StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task SecondAdmin_CannotDemoteOrDeleteThemselves()
    {
        // The "last admin" rule itself is unit-tested (an acting admin always exists, so over
        // HTTP it is only reachable through the self rules, which this exercises).
        using var client = _factory.CreateClient();
        var (_, secondAdminId) = await RegisterAndLoginAsync(client, approved: true, out var email);
        var adminToken = await LoginAdminAsync(client);
        await Send(client, HttpMethod.Put, $"/api/admin/users/{secondAdminId}/role", adminToken, new SetUserRoleRequestDto("Admin"));
        var secondToken = await LoginAsync(client, email, Password);

        Assert.That((await Send(client, HttpMethod.Put, $"/api/admin/users/{secondAdminId}/role", secondToken, new SetUserRoleRequestDto("Pilot"))).StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That((await Send(client, HttpMethod.Delete, $"/api/admin/users/{secondAdminId}", secondToken)).StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        await Send(client, HttpMethod.Put, $"/api/admin/users/{secondAdminId}/role", adminToken, new SetUserRoleRequestDto("Pilot"));
    }

    // ── block ──
    [Test]
    public async Task Admin_CanBlockAndUnblock_ButNotSelf()
    {
        using var client = _factory.CreateClient();
        var (_, pilotId) = await RegisterAndLoginAsync(client, approved: true, out var email);
        var adminToken = await LoginAdminAsync(client);

        Assert.That((await Send(client, HttpMethod.Post, $"/api/admin/users/{pilotId}/block", adminToken)).StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        Assert.That((await ListUsersAsync(client, adminToken)).Single(u => u.Id == pilotId).IsBlocked, Is.True);
        var blockedLogin = await client.PostAsJsonAsync("/api/authentication/login", new LoginRequestDto(email, Password));
        Assert.That(blockedLogin.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));

        Assert.That((await Send(client, HttpMethod.Post, $"/api/admin/users/{pilotId}/unblock", adminToken)).StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        var okLogin = await client.PostAsJsonAsync("/api/authentication/login", new LoginRequestDto(email, Password));
        Assert.That(okLogin.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var selfId = await AdminIdAsync(client, adminToken);
        Assert.That((await Send(client, HttpMethod.Post, $"/api/admin/users/{selfId}/block", adminToken)).StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That((await Send(client, HttpMethod.Post, $"/api/admin/users/{Guid.NewGuid()}/block", adminToken)).StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    // ── delete ──
    [Test]
    public async Task Admin_CanDeleteUser_RemovingTheirSessions_ButNotSelf()
    {
        using var client = _factory.CreateClient();
        var (_, pilotId) = await RegisterAndLoginAsync(client, approved: false);
        var adminToken = await LoginAdminAsync(client);
        await Send(client, HttpMethod.Post, $"/api/admin/users/{pilotId}/reject", adminToken);

        Assert.That((await Send(client, HttpMethod.Delete, $"/api/admin/users/{pilotId}", adminToken)).StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        Assert.That((await ListUsersAsync(client, adminToken)).Any(u => u.Id == pilotId), Is.False);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Nimbus.Infrastructure.Persistence.AppDbContext>();
        Assert.That(db.RefreshTokens.Any(t => t.UserId == pilotId.ToString()), Is.False, "refresh tokens must not be orphaned");

        var selfId = await AdminIdAsync(client, adminToken);
        Assert.That((await Send(client, HttpMethod.Delete, $"/api/admin/users/{selfId}", adminToken)).StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That((await Send(client, HttpMethod.Delete, $"/api/admin/users/{Guid.NewGuid()}", adminToken)).StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    // ── login events ──
    [Test]
    public async Task LoginEvents_AreRecorded_MaskedAndBounded()
    {
        using var client = _factory.CreateClient();
        var (_, _) = await RegisterAndLoginAsync(client, approved: true, out var email);
        await client.PostAsJsonAsync("/api/authentication/login", new LoginRequestDto(email, "wrong-password"));
        await client.PostAsJsonAsync("/api/authentication/login", new LoginRequestDto("nobody@example.test", "x"));
        var adminToken = await LoginAdminAsync(client);

        var events = await (await Send(client, HttpMethod.Get, "/api/admin/login-events?limit=5", adminToken)).Content.ReadFromJsonAsync<List<LoginEventDto>>();
        Assert.That(events, Has.Count.LessThanOrEqualTo(5));
        Assert.That(events, Is.Ordered.Descending.By(nameof(LoginEventDto.OccurredAt)));
        Assert.That(events!.Any(e => e is { Succeeded: true }), Is.True);
        Assert.That(events.Any(e => e is { Succeeded: false, UserEmail: null }), Is.True, "unknown account: no email stored");
        Assert.That(events.Where(e => e.MaskedIp is not null).All(e => e.MaskedIp!.EndsWith(".x.x") || e.MaskedIp.EndsWith(":x:x")), Is.True);

        Assert.That((await Send(client, HttpMethod.Get, "/api/admin/login-events?limit=0", adminToken)).StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity));
        Assert.That((await Send(client, HttpMethod.Get, "/api/admin/login-events?limit=201", adminToken)).StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity));
    }

    // ── access tokens follow the account's CURRENT state ──
    [Test]
    public async Task BlockedAdmin_TokenIsRejected_SoSelfUnblockFails()
    {
        using var client = _factory.CreateClient();
        var victim = await _factory.CreateUserAsync("Admin");
        var victimToken = await LoginAsAsync(client, victim);
        var adminToken = await LoginAdminAsync(client);

        Assert.That((await Send(client, HttpMethod.Get, "/api/admin/users", victimToken)).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await Send(client, HttpMethod.Post, $"/api/admin/users/{victim.Id}/block", adminToken)).StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        Assert.That((await Send(client, HttpMethod.Get, "/api/admin/users", victimToken)).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That((await Send(client, HttpMethod.Post, $"/api/admin/users/{victim.Id}/unblock", victimToken)).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That((await ListUsersAsync(client, adminToken)).Single(u => u.Id == victim.Id).IsBlocked, Is.True);
    }

    [Test]
    public async Task DemotedAdmin_CannotReusePreviousTokenToActOrRePromote()
    {
        using var client = _factory.CreateClient();
        var demoted = await _factory.CreateUserAsync("Admin");
        var demotedToken = await LoginAsAsync(client, demoted);
        var adminToken = await LoginAdminAsync(client);

        Assert.That((await Send(client, HttpMethod.Put, $"/api/admin/users/{demoted.Id}/role", adminToken, new SetUserRoleRequestDto("Pilot"))).StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        Assert.That((await Send(client, HttpMethod.Put, $"/api/admin/users/{demoted.Id}/role", demotedToken, new SetUserRoleRequestDto("Admin"))).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        // A fresh login carries the Pilot role and is forbidden from the admin area.
        var fresh = await LoginAsAsync(client, demoted);
        Assert.That((await Send(client, HttpMethod.Get, "/api/admin/users", fresh)).StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task DeletedAdmin_TokenIsRejected()
    {
        using var client = _factory.CreateClient();
        var doomed = await _factory.CreateUserAsync("Admin");
        var doomedToken = await LoginAsAsync(client, doomed);
        var adminToken = await LoginAdminAsync(client);

        Assert.That((await Send(client, HttpMethod.Delete, $"/api/admin/users/{doomed.Id}", adminToken)).StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        Assert.That((await Send(client, HttpMethod.Get, "/api/admin/users", doomedToken)).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    // ── pending / rejected users are gated server-side ──
    [Test]
    public async Task PendingAndRejectedUsers_Get403_OnProtectedEndpoints_But_CanReadMe()
    {
        using var client = _factory.CreateClient();
        var pending = await _factory.CreateUserAsync(approved: false);
        var rejected = await _factory.CreateUserAsync(approved: false, rejected: true);
        var approved = await _factory.CreateUserAsync();
        var pendingAdmin = await _factory.CreateUserAsync("Admin", approved: false);

        foreach (var (user, expected) in new[]
                 {
                     (pending, HttpStatusCode.Forbidden), (rejected, HttpStatusCode.Forbidden),
                     (approved, HttpStatusCode.OK), (pendingAdmin, HttpStatusCode.Forbidden)
                 })
        {
            var token = await LoginAsAsync(client, user);
            Assert.That((await Send(client, HttpMethod.Get, "/api/test/protected", token)).StatusCode, Is.EqualTo(expected), user.Email);
            Assert.That((await Send(client, HttpMethod.Get, "/api/authentication/me", token)).StatusCode, Is.EqualTo(HttpStatusCode.OK), user.Email);
        }

        var pendingAdminToken = await LoginAsAsync(client, pendingAdmin);
        Assert.That((await Send(client, HttpMethod.Get, "/api/admin/users", pendingAdminToken)).StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task Approval_TakesEffectImmediately_ForAnExistingToken()
    {
        using var client = _factory.CreateClient();
        var pending = await _factory.CreateUserAsync(approved: false);
        var token = await LoginAsAsync(client, pending);
        var adminToken = await LoginAdminAsync(client);
        Assert.That((await Send(client, HttpMethod.Get, "/api/test/protected", token)).StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));

        await Send(client, HttpMethod.Post, $"/api/admin/users/{pending.Id}/approve", adminToken);
        Assert.That((await Send(client, HttpMethod.Get, "/api/test/protected", token)).StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    // ── timestamps serialise as UTC ──
    [Test]
    public async Task Timestamps_AreSerialisedWithUtcDesignator()
    {
        using var client = _factory.CreateClient();
        await RegisterAndLoginAsync(client, approved: true);
        var adminToken = await LoginAdminAsync(client);

        var usersJson = await (await Send(client, HttpMethod.Get, "/api/admin/users", adminToken)).Content.ReadAsStringAsync();
        var eventsJson = await (await Send(client, HttpMethod.Get, "/api/admin/login-events?limit=5", adminToken)).Content.ReadAsStringAsync();
        Assert.That(usersJson, Does.Match("\"requestedAt\":\"[^\"]+Z\""));
        Assert.That(eventsJson, Does.Match("\"occurredAt\":\"[^\"]+Z\""));
    }

    // ── retention ──
    [Test]
    public async Task LoginEventRetention_PurgesOnlyEventsOlderThanTheWindow()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Nimbus.Infrastructure.Persistence.AppDbContext>();
            db.LoginEvents.Add(new Nimbus.Domain.Entities.LoginEvent(DateTime.UtcNow.AddDays(-400), null, null, false, "9.9.x.x"));
            db.LoginEvents.Add(new Nimbus.Domain.Entities.LoginEvent(DateTime.UtcNow.AddDays(-1), null, null, false, "8.8.x.x"));
            await db.SaveChangesAsync();
        }

        var purger = _factory.Services.GetServices<Microsoft.Extensions.Hosting.IHostedService>()
            .OfType<Nimbus.Infrastructure.Persistence.LoginEventRetentionService>().Single();
        var deleted = await purger.PurgeAsync(CancellationToken.None);

        using var check = _factory.Services.CreateScope();
        var ips = check.ServiceProvider.GetRequiredService<Nimbus.Infrastructure.Persistence.AppDbContext>().LoginEvents.Select(e => e.MaskedIp).ToList();
        Assert.That(deleted, Is.GreaterThanOrEqualTo(1));
        Assert.That(ips, Does.Not.Contain("9.9.x.x"));
        Assert.That(ips, Does.Contain("8.8.x.x"));
    }

    private static async Task<HttpResponseMessage> Send(HttpClient client, HttpMethod method, string url, string token, object? body = null)
    {
        using var req = Authed(method, url, token);
        if (body is not null)
        {
            req.Content = JsonContent.Create(body);
        }
        return await client.SendAsync(req);
    }

    private static async Task<List<AdminUserDto>> ListUsersAsync(HttpClient client, string adminToken)
    {
        return (await (await Send(client, HttpMethod.Get, "/api/admin/users", adminToken)).Content.ReadFromJsonAsync<List<AdminUserDto>>())!;
    }

    private static async Task<Guid> AdminIdAsync(HttpClient client, string adminToken)
    {
        var me = await (await Send(client, HttpMethod.Get, "/api/authentication/me", adminToken)).Content.ReadFromJsonAsync<CurrentUserDto>();
        return Guid.Parse(me!.Id);
    }

    private static async Task<string> LoginAsync(HttpClient client, string email, string password)
    {
        var res = await client.PostAsJsonAsync("/api/authentication/login", new LoginRequestDto(email, password));
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<LoginResponseDto>())!.AccessToken;
    }

    private Task<(string Token, Guid Id)> RegisterAndLoginAsync(HttpClient client, bool approved, out string email)
    {
        var e = $"user-{Guid.NewGuid():N}@example.test";
        email = e;
        return RegisterAndLoginCoreAsync(client, approved, e);
    }

    private static HttpRequestMessage Authed(HttpMethod method, string url, string token)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return req;
    }

    // The seeded admin's credential is generated per factory (see NimbusApiFactory).
    private Task<string> LoginAdminAsync(HttpClient client)
    {
        return LoginAsync(client, _factory.AdminEmail, _factory.AdminPassword);
    }

    private async Task<string> LoginAsAsync(HttpClient client, TestUser user)
    {
        return await LoginAsync(client, user.Email, user.Password);
    }

    private Task<(string Token, Guid Id)> RegisterAndLoginAsync(HttpClient client, bool approved)
    {
        return RegisterAndLoginCoreAsync(client, approved, $"user-{Guid.NewGuid():N}@example.test");
    }

    private async Task<(string Token, Guid Id)> RegisterAndLoginCoreAsync(HttpClient client, bool approved, string email)
    {
        (await client.PostAsJsonAsync("/api/authentication/register",
            new RegisterRequestDto(email, Password, "Jane", "Doe"))).EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var um = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = (await um.FindByEmailAsync(email))!;
        await um.ConfirmEmailAsync(user, await um.GenerateEmailConfirmationTokenAsync(user));
        Assert.That(user.Role, Is.EqualTo(Nimbus.Domain.Enums.UserRole.Pilot), "registered users must not default to Admin");
        user.IsApproved = approved;
        await um.UpdateAsync(user);

        var login = await client.PostAsJsonAsync("/api/authentication/login", new LoginRequestDto(email, Password));
        login.EnsureSuccessStatusCode();
        var body = (await login.Content.ReadFromJsonAsync<LoginResponseDto>())!;
        Assert.That(body.IsApproved, Is.EqualTo(approved), "login must report approval without blocking unapproved users");
        return (body.AccessToken, Guid.Parse(user.Id));
    }
}
