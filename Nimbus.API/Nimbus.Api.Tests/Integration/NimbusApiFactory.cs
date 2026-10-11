using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Nimbus.Infrastructure.Persistence;
using NUnit.Framework;
using Testcontainers.MsSql;
namespace Nimbus.Api.Tests.Integration;

/// <summary>
///     Boots the real <c>Nimbus.API</c> host in-process against a disposable SQL Server
///     instance (Testcontainers — same pattern as <c>AppDbContextMigrationTests</c> in
///     Nimbus.Infrastructure.Tests), so the auth integration tests exercise the actual
///     ASP.NET Core pipeline (cookie auth, antiforgery, cookie options, Identity lockout, etc.)
///     rather than calling handlers directly.
/// </summary>
public sealed class NimbusApiFactory : WebApplicationFactory<Program>
{
    private readonly MsSqlContainer _sqlServer =
        new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    /// <summary>
    ///     Starts the SQL Server container, forces host creation, and applies EF Core
    ///     migrations. Must be awaited (e.g. from <c>OneTimeSetUp</c>) before any test issues
    ///     HTTP requests against this factory.
    /// </summary>
    /// <summary>Credential of the admin created by the startup seed — generated per factory, never hard-coded.</summary>
    public string AdminEmail { get; } = $"admin-{Guid.NewGuid():N}@example.test";

    public string AdminPassword { get; } = $"Aa1!{Guid.NewGuid():N}";

    /// <param name="loginPermitLimit">Per-IP login limit per window. Relaxed by default because every test client shares one (unknown) IP.</param>
    /// <param name="trustedProxyNetwork">CIDR trusted to send X-Forwarded-For; null keeps the production default (loopback + private ranges).</param>
    public async Task InitializeAsync(int loginPermitLimit = 100_000, string? trustedProxyNetwork = null)
    {
        await _sqlServer.StartAsync();

        // Nimbus.API/Program.cs reads several settings (connection string, Auth:*) directly off
        // `builder.Configuration` *before* `builder.Build()` runs, so overrides registered via
        // WebApplicationFactory's ConfigureWebHost/ConfigureAppConfiguration (which only land in
        // the final merged configuration produced by Build()) arrive too late for those reads.
        // Environment variables don't have that problem: WebApplicationBuilder.CreateBuilder()
        // wires up AddEnvironmentVariables() immediately, so setting them on this process
        // before the host is built makes them visible to Program.cs's early reads too.
        Environment.SetEnvironmentVariable("ConnectionStrings__Database", _sqlServer.GetConnectionString());

        Environment.SetEnvironmentVariable("Auth__RememberMeDays", "7");

        // Email:Enabled already defaults to false in appsettings.json (NullEmailSender), so
        // register() never touches a real SMTP server here; kept explicit for clarity.
        Environment.SetEnvironmentVariable("Email__Enabled", "false");

        // The runtime seed reads its admin from configuration; the tests give it a generated one.
        Environment.SetEnvironmentVariable("Seed__AdminEmail", AdminEmail);
        Environment.SetEnvironmentVariable("Seed__AdminPassword", AdminPassword);
        Environment.SetEnvironmentVariable("RateLimiting__Login__PermitLimit", loginPermitLimit.ToString());
        Environment.SetEnvironmentVariable("ForwardedHeaders__KnownNetworks__0", trustedProxyNetwork);

        // Migrate with a standalone DbContext BEFORE ever touching `Server`/`Services` below.
        // Program.cs calls `await app.Services.SeedUsers()` as an integral part of its own
        // startup (before `app.Run()`), and that startup runs synchronously the moment `Server`
        // is first accessed — so if we migrated via the DI container's AppDbContext afterwards,
        // SeedUsers() would already have failed against a schema-less database (and, since that
        // failure is caught by Program.cs's top-level try/catch rather than surfacing as
        // WebApplicationFactory's expected HostAbortedException, the host would silently never
        // actually start, breaking every subsequent request in a confusing way).
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(_sqlServer.GetConnectionString());
        await using (var db = new AppDbContext(optionsBuilder.Options))
        {
            await db.Database.MigrateAsync();
        }

        // Force host creation now (rather than lazily on the first HTTP request) so any startup
        // failure surfaces during OneTimeSetUp instead of inside the first test.
        _ = Server;
    }

    // Outside Development the auth and antiforgery cookies are Secure (and antiforgery refuses a non-TLS
    // request), so tests talk to the in-memory server the way browsers reach production: over https.
    public NimbusApiFactory()
    {
        ClientOptions.BaseAddress = new Uri("https://localhost");
        // One client serves many users per test; each user's cookies live in a TestSession, not in a shared jar.
        ClientOptions.HandleCookies = false;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        // Test-only controller that sits behind the default [Authorize] policy.
        builder.ConfigureServices(services =>
        {
            services.AddControllers().AddApplicationPart(typeof(NimbusApiFactory).Assembly);
            // TestServer has no socket peer: pretend every request comes from a loopback proxy,
            // as it does behind Caddy, so ForwardedHeaders can be exercised.
            services.AddTransient<IStartupFilter, LoopbackPeerStartupFilter>();
        });
    }

    /// <summary>Waits until the background writer has persisted every login event accepted so far.</summary>
    public async Task DrainLoginEventsAsync()
    {
        var queue = Services.GetRequiredService<LoginEventQueue>();
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (queue.Pending > 0 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(20);
        }

        Assert.That(queue.Pending, Is.Zero, "login events were not drained in time");
    }

    /// <summary>Creates a confirmed user directly through Identity (bypassing e-mail) with a generated password.</summary>
    public async Task<TestUser> CreateUserAsync(string role = "Pilot", bool approved = true, bool rejected = false)
    {
        using var scope = Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<Nimbus.Infrastructure.Identity.ApplicationUser>>();
        var email = $"user-{Guid.NewGuid():N}@example.test";
        var password = $"Aa1!{Guid.NewGuid():N}";
        var user = new Nimbus.Infrastructure.Identity.ApplicationUser
        {
            UserName = email, Email = email, FirstName = "Test", Name = "User", EmailConfirmed = true,
            IsApproved = approved, IsRejected = rejected,
            Role = Enum.Parse<Nimbus.Domain.Enums.UserRole>(role)
        };
        (await users.CreateAsync(user, password)).Succeeded.ThrowIfFalse();
        (await users.AddToRoleAsync(user, role)).Succeeded.ThrowIfFalse();
        return new TestUser(Guid.Parse(user.Id), email, password);
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _sqlServer.DisposeAsync();
    }
}

public sealed record TestUser(Guid Id, string Email, string Password);

internal static class BoolExtensions
{
    public static void ThrowIfFalse(this bool value)
    {
        if (!value)
        {
            throw new InvalidOperationException("Test user setup failed.");
        }
    }
}

internal sealed class LoopbackPeerStartupFilter : IStartupFilter
{
    public Action<Microsoft.AspNetCore.Builder.IApplicationBuilder> Configure(Action<Microsoft.AspNetCore.Builder.IApplicationBuilder> next)
    {
        return app =>
        {
            app.Use((context, nxt) =>
            {
                context.Connection.RemoteIpAddress ??= System.Net.IPAddress.Loopback;
                return nxt(context);
            });
            next(app);
        };
    }
}
