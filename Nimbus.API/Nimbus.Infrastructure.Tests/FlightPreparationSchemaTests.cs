using Microsoft.EntityFrameworkCore;
using Nimbus.Domain.Entities;
using Nimbus.Domain.Enums;
using Nimbus.Infrastructure.Persistence;
using Testcontainers.MsSql;
namespace Nimbus.Infrastructure.Tests;

/// <summary>
///     Integration tests for the flight preparation schema (issue #31): cascade delete, the
///     documented composite indexes, and per-user isolation at the data layer (no query ever mixes
///     another owner's rows in). Run against a real SQL Server instance via Testcontainers, same
///     rationale as <see cref="AppDbContextMigrationTests" />.
/// </summary>
[TestFixture]
public class FlightPreparationSchemaTests
{
    private MsSqlContainer _sqlServer = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUpAsync()
    {
        _sqlServer = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
        await _sqlServer.StartAsync();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDownAsync()
    {
        await _sqlServer.DisposeAsync();
    }

    private AppDbContext CreateContext()
    {
        return new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(_sqlServer.GetConnectionString()).Options);
    }

    private static FlightPreparation NewFlightPreparation(Guid ownerId) => new()
    {
        Id = Guid.NewGuid(),
        OwnerId = ownerId,
        Title = "Weelde · avondvlucht",
        FlightDate = new DateOnly(2026, 8, 8),
        FlightTime = new TimeOnly(18, 45)
    };

    [Test]
    public async Task DeletingAFlightPreparation_CascadeDeletes_AllChildRows_LeavingNoOrphans()
    {
        var ownerId = Guid.NewGuid();
        var flightId = Guid.NewGuid();

        await using (var db = CreateContext())
        {
            var flight = NewFlightPreparation(ownerId);
            flight.Id = flightId;
            flight.Passengers.Add(new Passenger { Id = Guid.NewGuid(), Name = "J. Peeters", WeightKg = 78, Order = 0 });
            flight.Images.Add(new FlightPreparationImage
            {
                Id = Guid.NewGuid(), Section = FlightPreparationSection.Meteo, ObjectKey = "o/f/photo.jpg", ContentType = "image/jpeg", Order = 0
            });
            flight.WindLevels.Add(new WindLevel { Id = Guid.NewGuid(), AltitudeM = 300, DirectionDeg = 245, SpeedKt = 9, TemperatureC = 17, Order = 0 });
            flight.Shares.Add(new FlightPreparationShare { Id = Guid.NewGuid(), SharedWithUserId = Guid.NewGuid() });

            db.FlightPreparations.Add(flight);
            await db.SaveChangesAsync();
        }

        // Hard-delete at the database level (bypassing the app's soft-delete convention) to prove
        // the FK cascade itself leaves no orphaned child rows.
        await using (var db = CreateContext())
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM FlightPreparations WHERE Id = {flightId}");
        }

        await using (var db = CreateContext())
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(await db.FlightPreparations.IgnoreQueryFilters().AnyAsync(f => f.Id == flightId), Is.False);
                Assert.That(await db.Passengers.IgnoreQueryFilters().AnyAsync(p => p.FlightPreparationId == flightId), Is.False);
                Assert.That(await db.FlightPreparationImages.IgnoreQueryFilters().AnyAsync(i => i.FlightPreparationId == flightId), Is.False);
                Assert.That(await db.WindLevels.IgnoreQueryFilters().AnyAsync(w => w.FlightPreparationId == flightId), Is.False);
                Assert.That(await db.FlightPreparationShares.IgnoreQueryFilters().AnyAsync(s => s.FlightPreparationId == flightId), Is.False);
            }
        }
    }

    [Test]
    public async Task CompositeIndexes_Exist_ForImagesPassengersAndWindLevels()
    {
        await using var db = CreateContext();

        var indexNames = await db.Database.SqlQuery<string>(
            $"SELECT name AS [Value] FROM sys.indexes WHERE object_id IN (OBJECT_ID('FlightPreparationImages'), OBJECT_ID('Passengers'), OBJECT_ID('WindLevels'))"
        ).ToListAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(indexNames, Has.Some.Matches<string>(n => n.Contains("FlightPreparationId_Section_Order")),
                "Images need a composite index on (FlightPreparationId, Section, Order).");
            Assert.That(indexNames, Has.Some.Matches<string>(n => n.Contains("Passengers") || n.Contains("FlightPreparationId_Order")),
                "Passengers need a composite index on (FlightPreparationId, Order).");
        }

        var passengerIndexColumns = await db.Database.SqlQuery<string>(
            $"""
            SELECT i.name AS [Value]
            FROM sys.indexes i
            WHERE i.object_id = OBJECT_ID('Passengers') AND i.name LIKE '%FlightPreparationId_Order%'
            """
        ).ToListAsync();
        Assert.That(passengerIndexColumns, Is.Not.Empty);

        var windLevelIndexColumns = await db.Database.SqlQuery<string>(
            $"""
            SELECT i.name AS [Value]
            FROM sys.indexes i
            WHERE i.object_id = OBJECT_ID('WindLevels') AND i.name LIKE '%FlightPreparationId_Order%'
            """
        ).ToListAsync();
        Assert.That(windLevelIndexColumns, Is.Not.Empty);
    }

    [Test]
    public async Task ListingFlightPreparations_ScopedToOwner_NeverReturnsAnotherOwnersRows()
    {
        var ownerA = Guid.NewGuid();
        var ownerB = Guid.NewGuid();

        await using (var db = CreateContext())
        {
            db.FlightPreparations.Add(NewFlightPreparation(ownerA));
            db.FlightPreparations.Add(NewFlightPreparation(ownerB));
            await db.SaveChangesAsync();
        }

        await using (var db = CreateContext())
        {
            var ownerAFlights = await db.FlightPreparations.Where(f => f.OwnerId == ownerA).ToListAsync();
            var ownerBFlights = await db.FlightPreparations.Where(f => f.OwnerId == ownerB).ToListAsync();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(ownerAFlights, Has.Count.EqualTo(1));
                Assert.That(ownerBFlights, Has.Count.EqualTo(1));
                Assert.That(ownerAFlights.Select(f => f.OwnerId), Does.Not.Contain(ownerB));
                Assert.That(ownerBFlights.Select(f => f.OwnerId), Does.Not.Contain(ownerA));
            }
        }
    }

    [Test]
    public async Task DeletingOwnerAsFlightPreparation_DoesNotAffectAnotherOwnersFlight()
    {
        var ownerA = Guid.NewGuid();
        var ownerB = Guid.NewGuid();
        var ownerBFlightId = Guid.NewGuid();

        await using (var db = CreateContext())
        {
            var flightA = NewFlightPreparation(ownerA);
            var flightB = NewFlightPreparation(ownerB);
            flightB.Id = ownerBFlightId;
            db.FlightPreparations.AddRange(flightA, flightB);
            await db.SaveChangesAsync();

            db.FlightPreparations.Remove(flightA);
            await db.SaveChangesAsync();
        }

        await using (var db = CreateContext())
        {
            Assert.That(await db.FlightPreparations.AnyAsync(f => f.Id == ownerBFlightId), Is.True,
                "Deleting one owner's flight must never remove another owner's flight.");
        }
    }
}
