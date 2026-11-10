using Microsoft.EntityFrameworkCore;
using Nimbus.Domain.Entities;
using Nimbus.Domain.Enums;
using Nimbus.Domain.Interfaces;
using Nimbus.Infrastructure.Persistence;
using Nimbus.Infrastructure.Persistence.Repositories;
using Testcontainers.MsSql;
namespace Nimbus.Infrastructure.Tests;

/// <summary>Issue #41: who may read a flight, decided in the reader (against real SQL Server), and what the read-only view is built from.</summary>
[TestFixture]
public class FlightDetailReaderTests
{
    private MsSqlContainer _sqlServer = null!;
    private readonly Guid _owner = Guid.NewGuid();
    private readonly Guid _sharedWith = Guid.NewGuid();
    private readonly Guid _revoked = Guid.NewGuid();
    private readonly Guid _stranger = Guid.NewGuid();
    private readonly Guid _admin = Guid.NewGuid();
    private Guid _flight;
    private Guid _snapshotFlight;
    private Guid _deletedFlight;

    [OneTimeSetUp]
    public async Task OneTimeSetUpAsync()
    {
        _sqlServer = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
        await _sqlServer.StartAsync();
        await using var db = CreateContext();
        await db.Database.MigrateAsync();

        // The owner's reference data; the balloon and the location are soft-deleted, as an owner may do later.
        var balloon = new Balloon { OwnerId = _owner, Registration = "OO-OWN", BasketWeightKg = 50, BurnerWeightKg = 20, CylindersWeightKg = 30, IsDeleted = true };
        var location = new LaunchLocation { OwnerId = _owner, Name = "Weelde", IcaoCode = "EBWE", IsDeleted = true };
        var pilot = new Pilot { OwnerId = _owner, Name = "Alice", WeightKg = 80 };
        // The viewer's own, different, reference data must never leak into the owner's flight.
        db.AddRange(balloon, location, pilot,
            new Balloon { OwnerId = _sharedWith, Registration = "OO-VIEWER" }, new LaunchLocation { OwnerId = _sharedWith, Name = "Viewer field" });
        await db.SaveChangesAsync();

        var flight = NewFlight("F", balloon.Id, location.Id, pilot.Id);
        flight.Passengers = [new Passenger { Name = "Second", WeightKg = 70, Order = 1 }, new Passenger { Name = "First", WeightKg = 60, Order = 0 },
            new Passenger { Name = "Gone", WeightKg = 99, Order = 2, IsDeleted = true }];
        flight.WindLevels = [new WindLevel { AltitudeM = 500, DirectionDeg = 90, SpeedKt = 5, Order = 1 }, new WindLevel { AltitudeM = 100, DirectionDeg = 80, SpeedKt = 3, Order = 0 }];
        flight.Images =
        [
            new FlightPreparationImage { Section = FlightPreparationSection.Route, ObjectKey = "o/f/b.png", ContentType = "image/png", Order = 1 },
            new FlightPreparationImage { Section = FlightPreparationSection.Meteo, ObjectKey = "o/f/a.png", ContentType = "image/png", Order = 0 },
            new FlightPreparationImage { Section = FlightPreparationSection.Meteo, ObjectKey = "o/f/gone.png", ContentType = "image/png", Order = 2, IsDeleted = true }
        ];
        var snapshot = NewFlight("S", balloon.Id, location.Id, pilot.Id);
        (snapshot.BasketWeightKg, snapshot.BurnerWeightKg, snapshot.CylindersWeightKg) = (55m, 22m, 33m);
        var deleted = NewFlight("D", balloon.Id, location.Id, pilot.Id);
        deleted.IsDeleted = true;
        db.AddRange(flight, snapshot, deleted);
        await db.SaveChangesAsync();

        _flight = flight.Id;
        _snapshotFlight = snapshot.Id;
        _deletedFlight = deleted.Id;
        db.FlightPreparationShares.AddRange(
            new FlightPreparationShare { FlightPreparationId = _flight, SharedWithUserId = _sharedWith },
            new FlightPreparationShare { FlightPreparationId = _flight, SharedWithUserId = _revoked, IsDeleted = true });
        await db.SaveChangesAsync();
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDownAsync() => await _sqlServer.DisposeAsync();

    private FlightPreparation NewFlight(string title, Guid balloon, Guid location, Guid pilot) => new()
    {
        OwnerId = _owner, Title = title, BalloonId = balloon, LaunchLocationId = location, PilotId = pilot,
        FlightDate = new DateOnly(2026, 8, 8), FlightTime = new TimeOnly(18, 45), EnvelopeWeightKg = 100, TotalLiftKg = 500
    };

    private AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(_sqlServer.GetConnectionString()).Options);

    private async Task<FlightDetailResult> Get(Guid viewer, Guid flight, bool admin = false)
    {
        await using var db = CreateContext();
        return await new FlightDetailReader(db).GetAsync(viewer, admin, flight);
    }

    [Test]
    public async Task Owner_IsTheOwner()
    {
        var r = await Get(_owner, _flight);
        Assert.That(r.Access, Is.EqualTo(new FlightAccess(true, FlightViewerRole.Owner)));
        Assert.That(r.Data, Is.Not.Null);
    }

    [Test]
    public async Task SomeoneWithAnActiveShare_MayRead()
    {
        var r = await Get(_sharedWith, _flight);
        Assert.That(r.Access.Role, Is.EqualTo(FlightViewerRole.Shared));
        Assert.That(r.Data!.Flight.Id, Is.EqualTo(_flight));
    }

    [Test]
    public async Task ARevokedShare_GrantsNothing_ButTheFlightIsKnownToExist()
    {
        var r = await Get(_revoked, _flight);
        Assert.That(r.Access, Is.EqualTo(new FlightAccess(true, null)));
        Assert.That(r.Data, Is.Null, "no data leaves the reader for a viewer who may not see it");
    }

    [Test]
    public async Task AStranger_ExistsButMayNotRead()
    {
        var r = await Get(_stranger, _flight);
        Assert.That(r.Access, Is.EqualTo(new FlightAccess(true, null)));
        Assert.That(r.Data, Is.Null);
    }

    [Test]
    public async Task AnAdmin_ReadsWithoutAShare()
    {
        var r = await Get(_admin, _flight, admin: true);
        Assert.That(r.Access.Role, Is.EqualTo(FlightViewerRole.Admin));
        Assert.That(r.Data, Is.Not.Null);
    }

    [Test]
    public async Task AMissingOrSoftDeletedFlight_DoesNotExist_EvenForTheOwnerAndAnAdmin()
    {
        Assert.That((await Get(_owner, Guid.NewGuid())).Access.Exists, Is.False);
        Assert.That((await Get(_owner, _deletedFlight)).Access.Exists, Is.False);
        Assert.That((await Get(_admin, _deletedFlight, admin: true)).Access.Exists, Is.False);
    }

    [Test]
    public async Task GetAccess_AnswersWithoutLoadingTheFlight()
    {
        await using var db = CreateContext();
        var access = await new FlightDetailReader(db).GetAccessAsync(_sharedWith, false, _flight);
        Assert.That(access, Is.EqualTo(new FlightAccess(true, FlightViewerRole.Shared)));
        Assert.That(db.ChangeTracker.Entries(), Is.Empty);
    }

    [Test]
    public async Task Names_ComeFromTheOwnersData_EvenWhenSoftDeleted_NeverTheViewers()
    {
        var d = (await Get(_sharedWith, _flight)).Data!;
        Assert.That((d.BalloonRegistration, d.PilotName, d.LocationName, d.LocationIcaoCode), Is.EqualTo(("OO-OWN", "Alice", "Weelde", "EBWE")));
    }

    [Test]
    public async Task Children_AreOrdered_AndSoftDeletedOnesLeftOut()
    {
        var f = (await Get(_owner, _flight)).Data!.Flight;
        Assert.That(f.Passengers.Select(p => p.Name), Is.EqualTo(new[] { "First", "Second" }));
        Assert.That(f.WindLevels.Select(w => w.AltitudeM), Is.EqualTo(new[] { 100, 500 }));
        Assert.That(f.Images.Select(i => i.ObjectKey), Is.EqualTo(new[] { "o/f/a.png", "o/f/b.png" }));
    }

    [Test]
    public async Task Assessment_UsesTheFlightsSnapshot_AndFallsBackToTheBalloon()
    {
        var withoutSnapshot = (await Get(_owner, _flight)).Data!.Assessment;
        Assert.That((withoutSnapshot.BasketWeightKg, withoutSnapshot.BurnerWeightKg, withoutSnapshot.CylindersWeightKg), Is.EqualTo((50m, 20m, 30m)));
        Assert.That(withoutSnapshot.PilotWeightKg, Is.EqualTo(80m));
        Assert.That(withoutSnapshot.PassengerWeightsKg, Is.EqualTo(new[] { 60m, 70m }));

        var withSnapshot = (await Get(_owner, _snapshotFlight)).Data!.Assessment;
        Assert.That((withSnapshot.BasketWeightKg, withSnapshot.BurnerWeightKg, withSnapshot.CylindersWeightKg), Is.EqualTo((55m, 22m, 33m)));
    }
}
