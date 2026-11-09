using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Nimbus.Domain.Entities;
using Nimbus.Domain.Enums;
using Nimbus.Domain.Interfaces;
using Nimbus.Infrastructure.Identity;
using Nimbus.Infrastructure.Persistence;
using Nimbus.Infrastructure.Persistence.Repositories;
using Testcontainers.MsSql;
namespace Nimbus.Infrastructure.Tests;

/// <summary>Issue #40: SQL-side filtering/sorting/paging, lightweight projection, per-user scoping and bounded query count with ~300 flights.</summary>
[TestFixture]
public class FlightListReaderTests
{
    private const int FlightsPerOwner = 300;
    private MsSqlContainer _sqlServer = null!;
    private readonly Guid _alice = Guid.NewGuid();
    private readonly Guid _bob = Guid.NewGuid();
    private readonly Guid _carol = Guid.NewGuid();
    private Guid _bobSharedWithAlice;
    private Guid _bobNotShared;

    [OneTimeSetUp]
    public async Task OneTimeSetUpAsync()
    {
        _sqlServer = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
        await _sqlServer.StartAsync();
        await using var db = CreateContext();
        await db.Database.MigrateAsync();

        var balloonA = new Balloon { OwnerId = _alice, Registration = "OO-AAA", BasketWeightKg = 50, BurnerWeightKg = 20, CylindersWeightKg = 30 };
        var balloonB = new Balloon { OwnerId = _bob, Registration = "OO-BBB" };
        var locA = new LaunchLocation { OwnerId = _alice, Name = "Weelde" };
        var locB = new LaunchLocation { OwnerId = _bob, Name = "Retie" };
        var pilotA = new Pilot { OwnerId = _alice, Name = "A", WeightKg = 80 };
        db.AddRange(balloonA, balloonB, locA, locB, pilotA);
        await db.SaveChangesAsync();

        var flights = new List<FlightPreparation>();
        for (var i = 0; i < FlightsPerOwner; i++)
        {
            flights.Add(Flight(_alice, i, balloonA.Id, locA.Id, pilotA.Id));
            flights.Add(Flight(_bob, i, balloonB.Id, locB.Id, null));
        }
        flights.Add(Flight(_carol, 0, null, null, null));
        // One flight carries its own snapshot of the balloon weights, deliberately unlike the balloon's current ones.
        var snapshotted = flights.First(f => f.OwnerId == _alice && f.Title == "F1");
        (snapshotted.BasketWeightKg, snapshotted.BurnerWeightKg, snapshotted.CylindersWeightKg) = (55m, 22m, 33m);
        db.AddRange(flights);
        await db.SaveChangesAsync();

        _bobSharedWithAlice = flights.First(f => f.OwnerId == _bob && f.Title == "F7").Id;
        _bobNotShared = flights.First(f => f.OwnerId == _bob && f.Title == "F8").Id;
        db.FlightPreparationShares.Add(new FlightPreparationShare { FlightPreparationId = _bobSharedWithAlice, SharedWithUserId = _alice });
        // A revoked share must not grant access.
        db.FlightPreparationShares.Add(new FlightPreparationShare { FlightPreparationId = _bobNotShared, SharedWithUserId = _alice, IsDeleted = true });
        db.Passengers.Add(new Passenger { FlightPreparationId = flights.First(f => f.OwnerId == _alice).Id, Name = "P", WeightKg = 70, Order = 0 });
        db.Users.Add(new ApplicationUser { Id = _bob.ToString(), UserName = "bob", FirstName = "Bob", Name = "Builder", Email = "bob@example.test" });
        await db.SaveChangesAsync();
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDownAsync() => await _sqlServer.DisposeAsync();

    private static FlightPreparation Flight(Guid owner, int i, Guid? balloon, Guid? location, Guid? pilot) => new()
    {
        OwnerId = owner, Title = $"F{i}", BalloonId = balloon, LaunchLocationId = location, PilotId = pilot,
        FlightDate = new DateOnly(2026, 1, 1).AddDays(i), FlightTime = new TimeOnly(7, 0),
        Status = i % 3 == 0 ? FlightPreparationStatus.Flown : FlightPreparationStatus.Planned,
        TotalLiftKg = 500, WindSpeedKt = 5, VisibilityKm = 10, CapeJPerKg = 0
    };

    private AppDbContext CreateContext(params IInterceptor[] interceptors) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(_sqlServer.GetConnectionString()).AddInterceptors(interceptors).Options);

    private static FlightListCriteria Criteria(Guid user, FlightListFilter filter = FlightListFilter.All, FlightListSort sort = FlightListSort.Date,
        bool desc = true, int page = 1, int size = 25, bool admin = false) => new(user, admin, filter, sort, desc, page, size);

    private async Task<FlightListPage> Run(FlightListCriteria c)
    {
        await using var db = CreateContext();
        return await new FlightListReader(db).GetPageAsync(c);
    }

    [Test]
    public async Task User_SeesOwnFlightsPlusActiveSharesOnly()
    {
        var page = await Run(Criteria(_alice, size: 100));
        var all = new List<FlightListRow>();
        for (var p = 1; p <= 4; p++) all.AddRange((await Run(Criteria(_alice, page: p, size: 100))).Items);

        Assert.That(page.TotalCount, Is.EqualTo(FlightsPerOwner + 1));
        Assert.That(all.Count(r => r.OwnerId == _alice), Is.EqualTo(FlightsPerOwner));
        Assert.That(all.Where(r => r.OwnerId != _alice).Select(r => r.Id), Is.EqualTo(new[] { _bobSharedWithAlice }));
        Assert.That(all.Any(r => r.OwnerId == _carol || r.Id == _bobNotShared), Is.False);
    }

    [Test]
    public async Task UserWithoutFlightsOrShares_SeesNothing_EvenWithSharedFilter()
    {
        var stranger = Guid.NewGuid();
        foreach (var f in Enum.GetValues<FlightListFilter>())
            Assert.That((await Run(Criteria(stranger, f))).TotalCount, Is.Zero);
    }

    [Test]
    public async Task Admin_SeesEveryFlight()
    {
        Assert.That((await Run(Criteria(Guid.NewGuid(), admin: true))).TotalCount, Is.EqualTo(FlightsPerOwner * 2 + 1));
    }

    [Test]
    public async Task Filters_FlownNotFlownAndSharedWithMe()
    {
        var flown = await Run(Criteria(_alice, FlightListFilter.Flown, size: 100));
        var notFlown = await Run(Criteria(_alice, FlightListFilter.NotFlown, size: 100));
        var shared = await Run(Criteria(_alice, FlightListFilter.SharedWithMe));

        Assert.That(flown.Items.All(r => r.Status == FlightPreparationStatus.Flown), Is.True);
        Assert.That(notFlown.Items.All(r => r.Status == FlightPreparationStatus.Planned), Is.True);
        Assert.That(flown.TotalCount + notFlown.TotalCount, Is.EqualTo(FlightsPerOwner + 1));
        Assert.That(shared.Items.Select(r => r.Id), Is.EqualTo(new[] { _bobSharedWithAlice }));
    }

    [Test]
    public async Task Paging_ReturnsDisjointPagesInSqlOrder_AndTotal()
    {
        var p1 = await Run(Criteria(_alice, size: 50, page: 1));
        var p2 = await Run(Criteria(_alice, size: 50, page: 2));

        Assert.That(p1.Items, Has.Count.EqualTo(50));
        Assert.That(p1.TotalCount, Is.EqualTo(FlightsPerOwner + 1));
        Assert.That(p1.Items.Select(r => r.Id).Intersect(p2.Items.Select(r => r.Id)), Is.Empty);
        Assert.That(p1.Items.Select(r => r.FlightDate), Is.Ordered.Descending);
        Assert.That(p1.Items[^1].FlightDate, Is.GreaterThanOrEqualTo(p2.Items[0].FlightDate));
    }

    [Test]
    public async Task Sorting_ByBalloonAndLocation_RunsInSql()
    {
        var byBalloon = await Run(Criteria(_alice, sort: FlightListSort.Balloon, desc: false, size: 100));
        Assert.That(byBalloon.Items.Select(r => r.BalloonRegistration ?? ""), Is.Ordered.Ascending);
        var byLocation = await Run(Criteria(_alice, sort: FlightListSort.Location, desc: true, size: 100));
        Assert.That(byLocation.Items.Select(r => r.LocationName ?? ""), Is.Ordered.Descending);
    }

    [Test]
    public async Task Rows_CarryBalloonLocationAndAssessmentInput_FromTheOwnersReferenceData()
    {
        var row = (await Run(Criteria(_alice, sort: FlightListSort.Date, desc: false, size: 1))).Items[0];
        Assert.That(row.BalloonRegistration, Is.EqualTo("OO-AAA"));
        Assert.That(row.LocationName, Is.EqualTo("Weelde"));
        Assert.That(row.Assessment.PilotWeightKg, Is.EqualTo(80));
        Assert.That(row.Assessment.BasketWeightKg, Is.EqualTo(50));
        Assert.That(row.Assessment.PassengerWeightsKg, Is.EqualTo(new[] { 70m }));
    }

    [Test]
    public async Task Rows_UseTheFlightsOwnWeightSnapshot_AndFallBackToTheBalloonWhenThereIsNone()
    {
        var items = (await Run(Criteria(_alice, sort: FlightListSort.Date, desc: false, size: 5))).Items;
        var snapshotted = items.Single(r => r.Title == "F1").Assessment;
        Assert.That((snapshotted.BasketWeightKg, snapshotted.BurnerWeightKg, snapshotted.CylindersWeightKg), Is.EqualTo((55m, 22m, 33m)));
        var legacy = items.Single(r => r.Title == "F0").Assessment;
        Assert.That((legacy.BasketWeightKg, legacy.BurnerWeightKg, legacy.CylindersWeightKg), Is.EqualTo((50m, 20m, 30m)));
    }

    [Test]
    public async Task ThreeHundredFlights_BoundedQueryCount_AndNoChildOrWideEntityLoad()
    {
        var counter = new CommandCounter();
        await using var db = CreateContext(counter);
        var page = await new FlightListReader(db).GetPageAsync(Criteria(_alice, size: 50));

        Assert.That(page.Items, Has.Count.EqualTo(50));
        Assert.That(counter.Commands, Has.Count.EqualTo(3), "count + paged projection + passenger weights, regardless of rows");
        Assert.That(db.ChangeTracker.Entries().Count(), Is.Zero, "nothing tracked: no entities materialised");
        var projection = counter.Commands.Single(c => c.Contains("OFFSET"));
        Assert.That(projection, Does.Not.Contain("BulletinText").And.Not.Contain("TrajectoryJson").And.Not.Contain("PaxBriefingText"));
        Assert.That(counter.Commands.Count(c => c.Contains("FROM [Passengers]")), Is.EqualTo(1));
    }

    [Test]
    public async Task UserDirectory_ResolvesManyOwnersInOneQuery()
    {
        var counter = new CommandCounter();
        await using var db = CreateContext(counter);
        var names = await new UserDirectory(db).GetDisplayNamesAsync([_bob, Guid.NewGuid(), Guid.NewGuid()]);

        Assert.That(counter.Commands, Has.Count.EqualTo(1));
        Assert.That(names[_bob], Is.EqualTo("Bob Builder"));
        Assert.That(names, Has.Count.EqualTo(1));
    }

    private sealed class CommandCounter : DbCommandInterceptor
    {
        public List<string> Commands { get; } = [];

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken ct = default)
        {
            Commands.Add(command.CommandText);
            return base.ReaderExecutingAsync(command, eventData, result, ct);
        }
    }
}
