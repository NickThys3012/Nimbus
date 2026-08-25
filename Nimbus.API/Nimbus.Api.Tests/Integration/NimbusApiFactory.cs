using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Nimbus.Infrastructure.Persistence;
using Testcontainers.MsSql;
namespace Nimbus.Api.Tests.Integration;

/// <summary>
///     Boots the real <c>Nimbus.API</c> host in-process against a disposable SQL Server
///     instance (Testcontainers — same pattern as <c>AppDbContextMigrationTests</c> in
///     Nimbus.Infrastructure.Tests), so the auth integration tests exercise the actual
///     ASP.NET Core pipeline (JWT bearer auth, cookie options, Identity lockout, etc.)
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
    public async Task InitializeAsync()
    {
        await _sqlServer.StartAsync();

        // Nimbus.API/Program.cs reads several settings (connection string, Jwt:*) directly off
        // `builder.Configuration` *before* `builder.Build()` runs, so overrides registered via
        // WebApplicationFactory's ConfigureWebHost/ConfigureAppConfiguration (which only land in
        // the final merged configuration produced by Build()) arrive too late for those reads.
        // Environment variables don't have that problem: WebApplicationBuilder.CreateBuilder()
        // wires up AddEnvironmentVariables() immediately, so setting them on this process
        // before the host is built makes them visible to Program.cs's early reads too.
        Environment.SetEnvironmentVariable("ConnectionStrings__Database", _sqlServer.GetConnectionString());

        // Fixed (not the "REPLACE-ME" placeholder) so tokens minted during a run validate
        // within that same run; irrelevant across runs since each run gets a fresh
        // container/database.
        Environment.SetEnvironmentVariable("Jwt__Secret", "integration-test-signing-key-at-least-32-chars-long");
        Environment.SetEnvironmentVariable("Jwt__Issuer", "Nimbus.Server.Tests");
        Environment.SetEnvironmentVariable("Jwt__Audience", "Nimbus.Client.Tests");
        Environment.SetEnvironmentVariable("Jwt__AccessTokenMinutes", "15");
        Environment.SetEnvironmentVariable("Jwt__RefreshTokenDays", "7");

        // Email:Enabled already defaults to false in appsettings.json (NullEmailSender), so
        // register() never touches a real SMTP server here; kept explicit for clarity.
        Environment.SetEnvironmentVariable("Email__Enabled", "false");

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

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _sqlServer.DisposeAsync();
    }
}
