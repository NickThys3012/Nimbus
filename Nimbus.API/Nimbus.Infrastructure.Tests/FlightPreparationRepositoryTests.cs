using Microsoft.EntityFrameworkCore;
using Nimbus.Contracts.DTOs.Features.FlightPreparations;
using Nimbus.Contracts.Mappers;
using Nimbus.Domain.Entities;
using Nimbus.Domain.Interfaces;
using Nimbus.Infrastructure.Persistence;
using Nimbus.Infrastructure.Persistence.Repositories;
using Testcontainers.MsSql;
namespace Nimbus.Infrastructure.Tests;

/// <summary>Transactional save, detach/restore and patch operations (issue #42), against real SQL Server.</summary>
[TestFixture]
public class FlightPreparationRepositoryTests
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

    private AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(_sqlServer.GetConnectionString()).Options);

    private static FlightPreparation NewFlight(Guid ownerId, int passengers = 3, int windLevels = 2)
    {
        var f = new FlightPreparation
        {
            OwnerId = ownerId,
            Title = "Weelde · avondvlucht",
            FlightDate = new DateOnly(2026, 8, 8),
            FlightTime = new TimeOnly(18, 45)
        };
        for (var i = 0; i < passengers; i++)
            f.Passengers.Add(new Passenger { Name = $"P{i}", WeightKg = 70 + i, FlightPreparation = f, Order = 99 });
        for (var i = 0; i < windLevels; i++)
            f.WindLevels.Add(new WindLevel { AltitudeM = i * 100, DirectionDeg = 90, SpeedKt = 5, FlightPreparation = f });
        return f;
    }

    [Test]
    public async Task Add_InsertsFlightThenChildrenWithGeneratedIdAndPreservedOrder()
    {
        var owner = Guid.NewGuid();
        var flight = NewFlight(owner);

        await using (var db = CreateContext())
            await new FlightPreparationRepository(db).AddAsync(flight);

        Assert.That(flight.Id, Is.Not.EqualTo(Guid.Empty));
        Assert.That(flight.Passengers, Has.Count.EqualTo(3));
        Assert.That(flight.Passengers.All(p => ReferenceEquals(p.FlightPreparation, flight)), Is.True);

        await using var read = CreateContext();
        var stored = await new FlightPreparationRepository(read).GetByIdAsync(owner, flight.Id);
        Assert.That(stored!.Passengers.Select(p => p.Name), Is.EqualTo(new[] { "P0", "P1", "P2" }));
        Assert.That(stored.Passengers.Select(p => p.Order), Is.EqualTo(new[] { 0, 1, 2 }));
        Assert.That(stored.Passengers.All(p => p.FlightPreparationId == flight.Id), Is.True);
        Assert.That(stored.WindLevels, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task LoadEditAndUpdate_ThroughTheReadOnlyGetter_ReplacesChildrenAndKeepsOutcomeColumns()
    {
        var owner = Guid.NewGuid();
        var flight = NewFlight(owner);
        flight.Status = Nimbus.Domain.Enums.FlightPreparationStatus.Flown;
        flight.FlownAt = new DateTime(2026, 8, 8, 20, 0, 0, DateTimeKind.Utc);
        flight.OutcomeRemarks = "smooth";
        flight.TrajectoryJson = "{}";
        await using (var db = CreateContext())
            await new FlightPreparationRepository(db).AddAsync(flight);

        // The handler's path: detached read, apply the form's request, hand the same object back.
        await using (var db = CreateContext())
        {
            var repo = new FlightPreparationRepository(db);
            var loaded = (await repo.GetByIdAsync(owner, flight.Id))!;
            new FlightPreparationRequestDto
            {
                Title = "Edited", BalloonId = Guid.NewGuid(), PilotId = Guid.NewGuid(), LaunchLocationId = Guid.NewGuid(),
                FlightDate = new DateOnly(2026, 9, 1), FlightTime = new TimeOnly(7, 0),
                Passengers = [new PassengerDto("Only", 70, false, false, false)],
                WindLevels = [new WindLevelDto(500, 180, 8, 5), new WindLevelDto(900, 200, 12, 3), new WindLevelDto(1500, 220, 15, 0)]
            }.ApplyTo(loaded);
            Assert.That(await repo.UpdateAsync(owner, loaded), Is.True);
        }

        await using var read = CreateContext();
        var stored = (await new FlightPreparationRepository(read).GetByIdAsync(owner, flight.Id))!;
        Assert.That(stored.Title, Is.EqualTo("Edited"));
        Assert.That(stored.Passengers.Select(p => p.Name), Is.EqualTo(new[] { "Only" }));
        Assert.That(stored.WindLevels.Select(w => w.AltitudeM), Is.EqualTo(new[] { 500, 900, 1500 }));
        Assert.That(stored.WindLevels.Select(w => w.Order), Is.EqualTo(new[] { 0, 1, 2 }));
        Assert.That(stored.Status, Is.EqualTo(Nimbus.Domain.Enums.FlightPreparationStatus.Flown));
        Assert.That(stored.FlownAt, Is.EqualTo(flight.FlownAt));
        Assert.That(stored.OutcomeRemarks, Is.EqualTo("smooth"));
        Assert.That(stored.TrajectoryJson, Is.EqualTo("{}"));
    }

    private async Task<FlightPreparation> SeedFullFlightAsync(Guid owner)
    {
        var flight = NewFlight(owner, passengers: 2, windLevels: 2);
        flight.KmlTrackObjectKey = $"{owner}/track.kml";
        await using (var db = CreateContext())
            await new FlightPreparationRepository(db).AddAsync(flight);

        await using var extra = CreateContext();
        extra.FlightPreparationImages.AddRange(
            new FlightPreparationImage { FlightPreparationId = flight.Id, ObjectKey = $"{owner}/b.png", ContentType = "image/png", Order = 1 },
            new FlightPreparationImage { FlightPreparationId = flight.Id, ObjectKey = $"{owner}/a.png", ContentType = "image/png", Order = 0 },
            new FlightPreparationImage { FlightPreparationId = flight.Id, ObjectKey = $"{owner}/gone.png", ContentType = "image/png", Order = 2, IsDeleted = true });
        extra.FlightPreparationShares.AddRange(
            new FlightPreparationShare { FlightPreparationId = flight.Id, SharedWithUserId = Guid.NewGuid() },
            new FlightPreparationShare { FlightPreparationId = flight.Id, SharedWithUserId = Guid.NewGuid(), IsDeleted = true });
        await extra.SaveChangesAsync();
        return flight;
    }

    private async Task<(int Flights, int Passengers, int WindLevels, int Images, int Shares)> CountsAsync(Guid flightId)
    {
        await using var db = CreateContext();
        return (
            await db.FlightPreparations.IgnoreQueryFilters().CountAsync(f => f.Id == flightId),
            await db.Passengers.IgnoreQueryFilters().CountAsync(p => p.FlightPreparationId == flightId),
            await db.WindLevels.IgnoreQueryFilters().CountAsync(w => w.FlightPreparationId == flightId),
            await db.FlightPreparationImages.IgnoreQueryFilters().CountAsync(i => i.FlightPreparationId == flightId),
            await db.FlightPreparationShares.IgnoreQueryFilters().CountAsync(sh => sh.FlightPreparationId == flightId));
    }

    [Test]
    public async Task Delete_RemovesTheFlightAndEverythingBelongingToIt_SoftDeletedRowsIncluded_AndReturnsTheFileKeys()
    {
        var owner = Guid.NewGuid();
        var flight = await SeedFullFlightAsync(owner);
        Assert.That(await CountsAsync(flight.Id), Is.EqualTo((1, 2, 2, 3, 2)));

        FlightDeletion? deletion;
        await using (var db = CreateContext())
            deletion = await new FlightPreparationRepository(db).DeleteAsync(owner, flight.Id);

        Assert.That(await CountsAsync(flight.Id), Is.EqualTo((0, 0, 0, 0, 0)), "nothing is left behind, not even soft-deleted rows");
        Assert.That(deletion!.ImageObjectKeys, Is.EqualTo(new[] { $"{owner}/a.png", $"{owner}/b.png", $"{owner}/gone.png" }));
        Assert.That(deletion.KmlTrackObjectKey, Is.EqualTo($"{owner}/track.kml"));
    }

    [Test]
    public async Task Delete_LeavesEveryOtherFlightUntouched()
    {
        var owner = Guid.NewGuid();
        var doomed = await SeedFullFlightAsync(owner);
        var sibling = await SeedFullFlightAsync(owner);
        var strangers = await SeedFullFlightAsync(Guid.NewGuid());

        await using (var db = CreateContext())
            await new FlightPreparationRepository(db).DeleteAsync(owner, doomed.Id);

        Assert.That(await CountsAsync(sibling.Id), Is.EqualTo((1, 2, 2, 3, 2)));
        Assert.That(await CountsAsync(strangers.Id), Is.EqualTo((1, 2, 2, 3, 2)));
    }

    [Test]
    public async Task Delete_AsSomeoneWhoIsNotTheOwner_OrOfAMissingFlight_DeletesNothingAndReturnsNull()
    {
        var owner = Guid.NewGuid();
        var flight = await SeedFullFlightAsync(owner);

        await using var db = CreateContext();
        var repo = new FlightPreparationRepository(db);
        Assert.That(await repo.DeleteAsync(Guid.NewGuid(), flight.Id), Is.Null);
        Assert.That(await repo.DeleteAsync(owner, Guid.NewGuid()), Is.Null);
        Assert.That(await CountsAsync(flight.Id), Is.EqualTo((1, 2, 2, 3, 2)));
    }

    [Test]
    public async Task Delete_OfASoftDeletedFlight_StillRemovesItForGood()
    {
        var owner = Guid.NewGuid();
        var flight = await SeedFullFlightAsync(owner);
        await using (var db = CreateContext())
            await db.FlightPreparations.IgnoreQueryFilters().Where(f => f.Id == flight.Id).ExecuteUpdateAsync(u => u.SetProperty(f => f.IsDeleted, true));

        await using (var db = CreateContext())
            Assert.That(await new FlightPreparationRepository(db).DeleteAsync(owner, flight.Id), Is.Not.Null);

        Assert.That(await CountsAsync(flight.Id), Is.EqualTo((0, 0, 0, 0, 0)));
    }

    [Test]
    public async Task Add_WhenChildInsertFails_RollsBackEverything_AndLeavesCallerObjectIntact()
    {
        var owner = Guid.NewGuid();
        var flight = NewFlight(owner);
        flight.Passengers.Last().Name = new string('x', 500); // exceeds the 200-char column: fails on the child insert
        var passengers = flight.Passengers;
        var firstPassenger = passengers.First();

        await using (var db = CreateContext())
            Assert.ThrowsAsync<DbUpdateException>(async () => await new FlightPreparationRepository(db).AddAsync(flight));

        Assert.That(flight.Id, Is.EqualTo(Guid.Empty), "generated id must be rolled back");
        Assert.That(flight.CreatedDate, Is.Null);
        Assert.That(flight.Passengers, Is.SameAs(passengers));
        Assert.That(flight.Passengers, Has.Count.EqualTo(3));
        Assert.That(flight.WindLevels, Has.Count.EqualTo(2));
        Assert.That(firstPassenger.FlightPreparation, Is.SameAs(flight));
        Assert.That(firstPassenger.FlightPreparationId, Is.EqualTo(Guid.Empty));
        Assert.That(firstPassenger.Order, Is.EqualTo(99));
        Assert.That(firstPassenger.Id, Is.EqualTo(Guid.Empty));

        await using var read = CreateContext();
        Assert.That(await read.FlightPreparations.AnyAsync(f => f.OwnerId == owner), Is.False, "the flight row must be rolled back");
    }

    [Test]
    public async Task Update_ReplacesChildren_PreservesOrder_AndKeepsPatchColumns()
    {
        var owner = Guid.NewGuid();
        var flight = NewFlight(owner);
        await using (var db = CreateContext())
        {
            var repo = new FlightPreparationRepository(db);
            await repo.AddAsync(flight);
            await repo.SetTrajectoryJsonAsync(owner, flight.Id, "{\"a\":1}");
            await repo.SetKmlTrackAsync(owner, flight.Id, "k/key.kml", "application/vnd.google-earth.kml+xml");
        }

        var edit = new FlightPreparation { Id = flight.Id, OwnerId = Guid.NewGuid(), Title = "Edited", FlightDate = flight.FlightDate, FlightTime = flight.FlightTime };
        edit.Passengers.Add(new Passenger { Name = "B", WeightKg = 80 });
        edit.Passengers.Add(new Passenger { Name = "A", WeightKg = 60 });

        await using (var db = CreateContext())
            Assert.That(await new FlightPreparationRepository(db).UpdateAsync(owner, edit), Is.True);

        await using var read = CreateContext();
        var stored = await new FlightPreparationRepository(read).GetByIdAsync(owner, flight.Id);
        Assert.That(stored!.Title, Is.EqualTo("Edited"));
        Assert.That(stored.OwnerId, Is.EqualTo(owner), "ownership comes from the caller's argument, never the payload");
        Assert.That(stored.Passengers.Select(p => p.Name), Is.EqualTo(new[] { "B", "A" }));
        Assert.That(stored.WindLevels, Is.Empty);
        Assert.That(stored.TrajectoryJson, Is.EqualTo("{\"a\":1}"));
        Assert.That(stored.KmlTrackObjectKey, Is.EqualTo("k/key.kml"));
        Assert.That(await read.Passengers.IgnoreQueryFilters().CountAsync(p => p.FlightPreparationId == flight.Id), Is.EqualTo(2));
    }

    [Test]
    public async Task Update_WhenChildInsertFails_RollsBack_AndLeavesCallerObjectIntact()
    {
        var owner = Guid.NewGuid();
        var flight = NewFlight(owner);
        await using (var db = CreateContext())
            await new FlightPreparationRepository(db).AddAsync(flight);

        var edit = NewFlight(owner, passengers: 2, windLevels: 1);
        edit.Id = flight.Id;
        edit.Title = "Should not persist";
        edit.Passengers.Last().Name = new string('x', 500);
        var first = edit.Passengers.First();

        await using (var db = CreateContext())
            Assert.ThrowsAsync<DbUpdateException>(async () => await new FlightPreparationRepository(db).UpdateAsync(owner, edit));

        Assert.That(edit.Title, Is.EqualTo("Should not persist"));
        Assert.That(edit.Passengers, Has.Count.EqualTo(2));
        Assert.That(edit.WindLevels, Has.Count.EqualTo(1));
        Assert.That(first.FlightPreparation, Is.SameAs(edit));
        Assert.That(first.Id, Is.EqualTo(Guid.Empty));
        Assert.That(first.Order, Is.EqualTo(99));

        await using var read = CreateContext();
        var stored = await new FlightPreparationRepository(read).GetByIdAsync(owner, flight.Id);
        Assert.That(stored!.Title, Is.EqualTo("Weelde · avondvlucht"));
        Assert.That(stored.Passengers.Select(p => p.Name), Is.EqualTo(new[] { "P0", "P1", "P2" }));
        Assert.That(stored.WindLevels, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task PatchOperations_UpdateOnlyTheirColumns_WithoutLoadingTheRow()
    {
        var owner = Guid.NewGuid();
        var flight = NewFlight(owner, 0, 0);
        await using var db = CreateContext();
        var repo = new FlightPreparationRepository(db);
        await repo.AddAsync(flight);
        db.ChangeTracker.Clear();

        Assert.That(await repo.SetTrajectoryJsonAsync(owner, flight.Id, "[1,2]"), Is.True);
        Assert.That(await repo.SetKmlTrackAsync(owner, flight.Id, "a/b.kml", "application/xml"), Is.True);
        Assert.That(db.ChangeTracker.Entries(), Is.Empty, "patching must not load the row");

        var stored = await db.FlightPreparations.AsNoTracking().SingleAsync(f => f.Id == flight.Id);
        Assert.That(stored.TrajectoryJson, Is.EqualTo("[1,2]"));
        Assert.That(stored.KmlTrackObjectKey, Is.EqualTo("a/b.kml"));
        Assert.That(stored.Title, Is.EqualTo(flight.Title));

        Assert.That(await repo.SetKmlTrackAsync(owner, flight.Id, null, null), Is.True);
        Assert.That((await db.FlightPreparations.AsNoTracking().SingleAsync(f => f.Id == flight.Id)).KmlTrackObjectKey, Is.Null);
    }

    [Test]
    public async Task UserA_CannotRead_Update_Or_Patch_UserBsFlight()
    {
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        var flight = NewFlight(userB);
        await using var db = CreateContext();
        var repo = new FlightPreparationRepository(db);
        await repo.AddAsync(flight);
        db.ChangeTracker.Clear();

        Assert.That(await repo.GetByIdAsync(userA, flight.Id), Is.Null);

        var edit = new FlightPreparation { Id = flight.Id, Title = "Hijacked", FlightDate = flight.FlightDate, FlightTime = flight.FlightTime };
        Assert.That(await repo.UpdateAsync(userA, edit), Is.False);
        Assert.That(await repo.SetTrajectoryJsonAsync(userA, flight.Id, "evil"), Is.False);
        Assert.That(await repo.SetKmlTrackAsync(userA, flight.Id, "evil", "x"), Is.False);

        var stored = await db.FlightPreparations.AsNoTracking().SingleAsync(f => f.Id == flight.Id);
        Assert.That(stored.Title, Is.EqualTo("Weelde · avondvlucht"));
        Assert.That(stored.TrajectoryJson, Is.Null);
        Assert.That(stored.KmlTrackObjectKey, Is.Null);
        Assert.That(stored.OwnerId, Is.EqualTo(userB));
    }
}
