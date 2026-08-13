using Microsoft.EntityFrameworkCore;
using Nimbus.Infrastructure.Persistence;
using Testcontainers.MsSql;
namespace Nimbus.Infrastructure.Tests;

/// <summary>
///     Integration tests for the EF Core migration path (issue #2), run against a real SQL Server
///     instance via Testcontainers rather than an in-memory provider — the in-memory provider does
///     not exercise SQL Server-specific migration behaviour (indexes, filtered indexes, providers'
///     SQL generation), so it cannot be trusted to prove a migration "applies cleanly".
/// </summary>
[TestFixture]
public class AppDbContextMigrationTests
{
    private MsSqlContainer _sqlServer = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUpAsync()
    {
        _sqlServer = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
        await _sqlServer.StartAsync();
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDownAsync()
    {
        await _sqlServer.DisposeAsync();
    }

    private AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(_sqlServer.GetConnectionString())
            .Options;

        return new AppDbContext(options);
    }

    [Test]
    public async Task Migrations_ApplyCleanly_ToAnEmptyDatabase()
    {
        await using var db = CreateContext();

        await db.Database.MigrateAsync();

        var applied = (await db.Database.GetAppliedMigrationsAsync()).ToList();
        var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();

        Assert.That(applied, Is.Not.Empty, "The initial migration should have been applied.");
        Assert.That(pending, Is.Empty, "No migrations should be pending after MigrateAsync completes.");
    }

    [Test]
    public async Task Migrations_AreIdempotent_WhenAppliedTwice()
    {
        await using var db = CreateContext();

        // The migrator container can retry after a transient failure (e.g. the database
        // container restarting mid-deploy) — re-running migrations against an
        // already-migrated database must be a no-op, not an error.
        await db.Database.MigrateAsync();
        Assert.DoesNotThrowAsync(async () => await db.Database.MigrateAsync());
    }
}
