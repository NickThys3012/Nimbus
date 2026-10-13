using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
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
        var (oldToken, pilotId) = await RegisterAndLoginAsync(client, approved: true, out var pilotEmail);
        var adminToken = await LoginAdminAsync(client);

        await SetApprovedAsync(pilotId, false); // only a pending account can be rejected
        Assert.That((await Send(client, HttpMethod.Post, $"/api/admin/users/{pilotId}/reject", adminToken)).StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        Assert.That((await Send(client, HttpMethod.Get, "/api/authentication/me", oldToken)).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized), "reject invalidates outstanding tokens");

        var rejectedLogin = await client.PostAsJsonAsync("/api/authentication/login", new LoginRequestDto(pilotEmail, Password));
        Assert.That(rejectedLogin.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized), "a rejected account cannot sign in");
        Assert.That(await rejectedLogin.Content.ReadAsStringAsync(), Is.EqualTo("Invalid credentials"));

        var listed = (await ListUsersAsync(client, adminToken)).Single(u => u.Id == pilotId);
        Assert.That(listed.IsRejected, Is.True);
        Assert.That(listed.IsApproved, Is.False);
        Assert.That(listed.RequestedAt, Is.Not.Null);

        Assert.That((await Send(client, HttpMethod.Post, $"/api/admin/users/{pilotId}/approve", adminToken)).StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

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

    // Flight ownership policy (docs/architecture.md, "Deleting users"): personal data cascades with the
    // account, other users' data is never touched, and security-audit rows are retained.
    [Test]
    public async Task DeleteUser_CascadesPersonalData_ButLeavesOtherUsersAndAuditRowsIntact()
    {
        using var client = _factory.CreateClient();
        var (_, doomedId) = await RegisterAndLoginAsync(client, approved: true, out var doomedEmail);
        var (_, survivorId) = await RegisterAndLoginAsync(client, approved: true);
        await _factory.DrainLoginEventsAsync();
        var adminToken = await LoginAdminAsync(client);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Nimbus.Infrastructure.Persistence.AppDbContext>();
        var liveBalloon = new Nimbus.Domain.Entities.Balloon { Registration = "OO-LIV", Type = "T", EnvelopeVolumeM3 = 1, MaxEnvelopeTemperatureC = 1, OwnerId = doomedId };
        var softDeletedBalloon = new Nimbus.Domain.Entities.Balloon { Registration = "OO-DEL", Type = "T", EnvelopeVolumeM3 = 1, MaxEnvelopeTemperatureC = 1, OwnerId = doomedId, IsDeleted = true };
        var survivorBalloon = new Nimbus.Domain.Entities.Balloon { Registration = "OO-LIV", Type = "T", EnvelopeVolumeM3 = 1, MaxEnvelopeTemperatureC = 1, OwnerId = survivorId };
        db.Balloons.AddRange(liveBalloon, softDeletedBalloon, survivorBalloon);
        var doomedPilot = new Nimbus.Domain.Entities.Pilot { Name = "Gone", WeightKg = 80m, OwnerId = doomedId, IsDeleted = true };
        var survivorPilot = new Nimbus.Domain.Entities.Pilot { Name = "Stays", WeightKg = 80m, OwnerId = survivorId };
        db.Pilots.AddRange(doomedPilot, survivorPilot);
        var doomedSpot = new Nimbus.Domain.Entities.LaunchLocation { Name = "Gone", Latitude = 50m, Longitude = 4m, OwnerId = doomedId, IsDeleted = true };
        var survivorSpot = new Nimbus.Domain.Entities.LaunchLocation { Name = "Stays", Latitude = 50m, Longitude = 4m, OwnerId = survivorId };
        db.LaunchLocations.AddRange(doomedSpot, survivorSpot);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        Assert.That((await Send(client, HttpMethod.Delete, $"/api/admin/users/{doomedId}", adminToken)).StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        Assert.That(db.RefreshTokens.Any(t => t.UserId == doomedId.ToString()), Is.False, "deleted user's personal data cascades");
        Assert.That(db.RefreshTokens.Any(t => t.UserId == survivorId.ToString()), Is.True, "another user's data is untouched");
        Assert.That(db.Balloons.IgnoreQueryFilters().Any(b => b.OwnerId == doomedId), Is.False, "balloons (including soft-deleted) cascade with the account");
        Assert.That(db.Balloons.IgnoreQueryFilters().Any(b => b.Id == survivorBalloon.Id), Is.True, "another user's balloons are untouched");
        Assert.That(db.Pilots.IgnoreQueryFilters().Any(p => p.OwnerId == doomedId), Is.False, "pilots (including soft-deleted) cascade with the account");
        Assert.That(db.Pilots.IgnoreQueryFilters().Any(p => p.Id == survivorPilot.Id), Is.True, "another user's pilots are untouched");
        Assert.That(db.LaunchLocations.IgnoreQueryFilters().Any(l => l.OwnerId == doomedId), Is.False, "launch locations (including soft-deleted) cascade with the account");
        Assert.That(db.LaunchLocations.IgnoreQueryFilters().Any(l => l.Id == survivorSpot.Id), Is.True, "another user's launch locations are untouched");
        Assert.That(db.Users.Any(u => u.Id == survivorId.ToString()), Is.True);
        Assert.That(db.LoginEvents.Any(e => e.Email == doomedEmail), Is.True, "audit trail is retained");
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
        await _factory.DrainLoginEventsAsync();

        var events = await (await Send(client, HttpMethod.Get, "/api/admin/login-events?limit=5", adminToken)).Content.ReadFromJsonAsync<List<LoginEventDto>>();
        Assert.That(events, Has.Count.LessThanOrEqualTo(5));
        Assert.That(events, Is.Ordered.Descending.By(nameof(LoginEventDto.OccurredAt)));
        Assert.That(events!.Any(e => e is { Succeeded: true }), Is.True);
        Assert.That(events.Any(e => e is { Succeeded: false, UserEmail: null, FailureReason: "InvalidPassword" }), Is.True, "unknown account: no email stored");
        Assert.That(events.Any(e => e is { Succeeded: false, UserEmail: not null, FailureReason: "InvalidPassword" }), Is.True, "wrong password for a known account");
        Assert.That(events.Where(e => e.Succeeded).All(e => e.FailureReason is null), Is.True);
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

    // ── pending / rejected users cannot sign in, and approval is re-checked on every request ──
    [Test]
    public async Task PendingAndRejectedUsers_CannotLogIn_WithTheSameResponseAsAWrongPassword()
    {
        using var client = _factory.CreateClient();
        var pending = await _factory.CreateUserAsync(approved: false);
        var rejected = await _factory.CreateUserAsync(approved: false, rejected: true);
        var pendingAdmin = await _factory.CreateUserAsync("Admin", approved: false);
        var approved = await _factory.CreateUserAsync();

        foreach (var user in new[] { pending, rejected, pendingAdmin })
        {
            var res = await client.PostAsJsonAsync("/api/authentication/login", new LoginRequestDto(user.Email, user.Password));
            Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized), user.Email);
            Assert.That(await res.Content.ReadAsStringAsync(), Is.EqualTo("Invalid credentials"), user.Email);
            Assert.That(res.Headers.Contains("Set-Cookie"), Is.False, user.Email);
        }

        var token = await LoginAsAsync(client, approved);
        Assert.That((await Send(client, HttpMethod.Get, "/api/test/protected", token)).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await Send(client, HttpMethod.Get, "/api/authentication/me", token)).StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task LosingApproval_TakesEffectImmediately_ForAnExistingToken()
    {
        using var client = _factory.CreateClient();
        var user = await _factory.CreateUserAsync();
        var token = await LoginAsAsync(client, user);
        Assert.That((await Send(client, HttpMethod.Get, "/api/test/protected", token)).StatusCode, Is.EqualTo(HttpStatusCode.OK));

        await SetApprovedAsync(user.Id, false);
        Assert.That((await Send(client, HttpMethod.Get, "/api/test/protected", token)).StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));

        await SetApprovedAsync(user.Id, true);
        Assert.That((await Send(client, HttpMethod.Get, "/api/test/protected", token)).StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    private async Task SetApprovedAsync(Guid id, bool approved)
    {
        using var scope = _factory.Services.CreateScope();
        var um = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var u = (await um.FindByIdAsync(id.ToString()))!;
        u.IsApproved = approved;
        await um.UpdateAsync(u);
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

    // ── audit recording is failure-isolated ──
    [Test]
    public async Task FailedAuditInsert_DoesNotStopTheWriter_OrTheLogin()
    {
        var queue = _factory.Services.GetRequiredService<Nimbus.Infrastructure.Persistence.LoginEventQueue>();
        // MaskedIp is limited to 64 characters, so persisting this event fails.
        queue.TryEnqueue(new Nimbus.Domain.Entities.LoginEvent(DateTime.UtcNow, null, null, false, new string('x', 200),
            Nimbus.Domain.Enums.LoginFailureReason.InvalidPassword));
        await _factory.DrainLoginEventsAsync();

        using var client = _factory.CreateClient();
        await RegisterAndLoginAsync(client, approved: true);
        await _factory.DrainLoginEventsAsync();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Nimbus.Infrastructure.Persistence.AppDbContext>();
        Assert.That(db.LoginEvents.Any(e => e.Succeeded), Is.True, "the writer keeps persisting after a bad batch");
    }

    // ── refresh vs block ──
    private static async Task<string> LoginForRefreshCookieAsync(HttpClient client, TestUser user)
    {
        var res = await client.PostAsJsonAsync("/api/authentication/login", new LoginRequestDto(user.Email, user.Password));
        res.EnsureSuccessStatusCode();
        var line = res.Headers.GetValues("Set-Cookie").First(v => v.StartsWith("refreshToken=", StringComparison.OrdinalIgnoreCase));
        return line.Split(';')[0].Substring("refreshToken=".Length);
    }

    private static Task<HttpResponseMessage> RefreshAsync(HttpClient client, string cookie)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/authentication/refresh");
        req.Headers.Add("Cookie", $"refreshToken={cookie}");
        return client.SendAsync(req);
    }

    [Test]
    public async Task Refresh_AfterBlock_IsRejected_AndNothingSurvivesUnblock()
    {
        using var client = _factory.CreateClient();
        var user = await _factory.CreateUserAsync();
        var cookie = await LoginForRefreshCookieAsync(client, user);
        var adminToken = await LoginAdminAsync(client);

        await Send(client, HttpMethod.Post, $"/api/admin/users/{user.Id}/block", adminToken);
        Assert.That((await RefreshAsync(client, cookie)).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));

        await Send(client, HttpMethod.Post, $"/api/admin/users/{user.Id}/unblock", adminToken);
        Assert.That((await RefreshAsync(client, cookie)).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized), "token revoked by the block must not revive on unblock");
    }

    [Test]
    public async Task ConcurrentLoginAndBlock_NeverLeavesALiveRefreshToken()
    {
        using var client = _factory.CreateClient();
        var adminToken = await LoginAdminAsync(client);
        for (var i = 0; i < 8; i++)
        {
            var user = await _factory.CreateUserAsync();
            var login = client.PostAsJsonAsync("/api/authentication/login", new LoginRequestDto(user.Email, user.Password));
            var block = Send(client, HttpMethod.Post, $"/api/admin/users/{user.Id}/block", adminToken);
            await Task.WhenAll(login, block);
            Assert.That(block.Result.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<Nimbus.Infrastructure.Persistence.AppDbContext>();
                Assert.That(db.RefreshTokens.Count(t => t.UserId == user.Id.ToString() && !t.IsRevoked), Is.Zero, $"iteration {i}: a login minted a refresh token past the block");
            }

            await Send(client, HttpMethod.Post, $"/api/admin/users/{user.Id}/unblock", adminToken);
            if (login.Result.IsSuccessStatusCode)
            {
                var cookie = login.Result.Headers.GetValues("Set-Cookie").First(v => v.StartsWith("refreshToken=", StringComparison.OrdinalIgnoreCase)).Split(';')[0].Substring("refreshToken=".Length);
                Assert.That((await RefreshAsync(client, cookie)).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized), "the cookie from a login racing the block must be dead after unblock");
            }
        }
    }

    [Test]
    public async Task ParallelRefreshesOfTheSameCookie_AtMostOneSucceeds_AndNoSecondTokenSurvives()
    {
        using var client = _factory.CreateClient();
        for (var round = 0; round < 3; round++)
        {
            var user = await _factory.CreateUserAsync();
            var cookie = await LoginForRefreshCookieAsync(client, user);

            var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() => RefreshAsync(client, cookie))));

            Assert.That(results.Count(r => r.IsSuccessStatusCode), Is.LessThanOrEqualTo(1), "exactly one rotation may win");
            Assert.That(results.Count(r => r.IsSuccessStatusCode), Is.EqualTo(1));
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<Nimbus.Infrastructure.Persistence.AppDbContext>();
            // Losers are replays: per the reuse semantics they revoke the family, so nothing stays alive.
            Assert.That(db.RefreshTokens.Count(t => t.UserId == user.Id.ToString() && !t.IsRevoked), Is.LessThanOrEqualTo(1));
        }
    }

    [Test]
    public async Task Refresh_ForRejectedAccount_IsRejected()
    {
        using var client = _factory.CreateClient();
        var user = await _factory.CreateUserAsync();
        var cookie = await LoginForRefreshCookieAsync(client, user);
        var adminToken = await LoginAdminAsync(client);
        await SetApprovedAsync(user.Id, false); // only a pending account can be rejected
        Assert.That((await Send(client, HttpMethod.Post, $"/api/admin/users/{user.Id}/reject", adminToken)).StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        Assert.That((await RefreshAsync(client, cookie)).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task ConcurrentRefreshAndBlock_NeverLeavesAUsableRefreshToken()
    {
        using var client = _factory.CreateClient();
        var adminToken = await LoginAdminAsync(client);
        for (var i = 0; i < 8; i++)
        {
            var user = await _factory.CreateUserAsync();
            var cookie = await LoginForRefreshCookieAsync(client, user);

            var refresh = RefreshAsync(client, cookie);
            var block = Send(client, HttpMethod.Post, $"/api/admin/users/{user.Id}/block", adminToken);
            await Task.WhenAll(refresh, block);
            Assert.That(block.Result.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<Nimbus.Infrastructure.Persistence.AppDbContext>();
            var live = db.RefreshTokens.Count(t => t.UserId == user.Id.ToString() && !t.IsRevoked);
            Assert.That(live, Is.Zero, $"iteration {i}: a refresh token survived the block");

            await Send(client, HttpMethod.Post, $"/api/admin/users/{user.Id}/unblock", adminToken);
            if (refresh.Result.IsSuccessStatusCode)
            {
                // The rotated cookie (if any) was minted before the block and must be dead now.
                var rotated = refresh.Result.Headers.GetValues("Set-Cookie").First(v => v.StartsWith("refreshToken=", StringComparison.OrdinalIgnoreCase)).Split(';')[0].Substring("refreshToken=".Length);
                Assert.That((await RefreshAsync(client, rotated)).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
            }
        }
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

        // Unapproved accounts cannot sign in (#16), so they have no token to hand back.
        if (!approved)
        {
            return (string.Empty, Guid.Parse(user.Id));
        }

        var login = await client.PostAsJsonAsync("/api/authentication/login", new LoginRequestDto(email, Password));
        login.EnsureSuccessStatusCode();
        var body = (await login.Content.ReadFromJsonAsync<LoginResponseDto>())!;
        Assert.That(body.IsApproved, Is.True);
        return (body.AccessToken, Guid.Parse(user.Id));
    }
}
