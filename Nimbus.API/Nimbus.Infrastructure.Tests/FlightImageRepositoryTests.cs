using Microsoft.EntityFrameworkCore;
using Nimbus.Domain.Entities;
using Nimbus.Domain.Enums;
using Nimbus.Infrastructure.Persistence;
using Nimbus.Infrastructure.Persistence.Repositories;
using Testcontainers.MsSql;
namespace Nimbus.Infrastructure.Tests;

/// <summary>Issue #50/#51: image rows are appended to their section, listed in order and re-ordered in one save, against real SQL Server.</summary>
[TestFixture]
public class FlightImageRepositoryTests
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

    private async Task<Guid> NewFlightAsync()
    {
        await using var db = CreateContext();
        var flight = new FlightPreparation { OwnerId = Guid.NewGuid(), Title = "t", FlightDate = new DateOnly(2026, 8, 8), FlightTime = new TimeOnly(18, 0) };
        db.FlightPreparations.Add(flight);
        await db.SaveChangesAsync();
        return flight.Id;
    }

    private async Task<Guid> AddAsync(Guid flight, FlightPreparationSection section)
    {
        await using var db = CreateContext();
        var image = new FlightPreparationImage
        {
            FlightPreparationId = flight, Section = section, ObjectKey = $"o/f/{Guid.NewGuid()}.png", ThumbnailObjectKey = "o/f/t.png", ContentType = "image/png", SizeBytes = 10
        };
        await new FlightImageRepository(db).AddAsync(image);
        return image.Id;
    }

    [Test]
    public async Task Add_AppendsToTheEndOfItsOwnSection()
    {
        var flight = await NewFlightAsync();
        var a = await AddAsync(flight, FlightPreparationSection.Meteo);
        var b = await AddAsync(flight, FlightPreparationSection.Meteo);
        var r = await AddAsync(flight, FlightPreparationSection.Route);

        await using var db = CreateContext();
        var list = await new FlightImageRepository(db).ListAsync(flight);

        Assert.That(list.Where(i => i.Section == FlightPreparationSection.Meteo).Select(i => (i.Id, i.Order)), Is.EqualTo(new[] { (a, 0), (b, 1) }));
        Assert.That(list.Single(i => i.Id == r).Order, Is.EqualTo(0));
        Assert.That(list.All(i => i.SizeBytes == 10 && i.ThumbnailObjectKey == "o/f/t.png" && i.CreatedDate != null));
    }

    [Test]
    public async Task SetOrder_WritesTheNewPositions_AndListFollowsThem()
    {
        var flight = await NewFlightAsync();
        var ids = new[] { await AddAsync(flight, FlightPreparationSection.Meteo), await AddAsync(flight, FlightPreparationSection.Meteo), await AddAsync(flight, FlightPreparationSection.Meteo) };

        await using (var db = CreateContext())
            await new FlightImageRepository(db).SetOrderAsync(flight, [ids[2], ids[0], ids[1]]);

        await using var read = CreateContext();
        var list = await new FlightImageRepository(read).ListAsync(flight);
        Assert.That(list.Select(i => i.Id), Is.EqualTo(new[] { ids[2], ids[0], ids[1] }));
    }

    [Test]
    public async Task List_IsPerFlight_AndDeleteRemovesTheRow()
    {
        var flight = await NewFlightAsync();
        var other = await NewFlightAsync();
        var mine = await AddAsync(flight, FlightPreparationSection.Meteo);
        await AddAsync(other, FlightPreparationSection.Meteo);

        await using var db = CreateContext();
        var repo = new FlightImageRepository(db);
        Assert.That((await repo.ListAsync(flight)).Select(i => i.Id), Is.EqualTo(new[] { mine }));
        Assert.That(await repo.GetAsync(other, mine), Is.Null, "an image is only reachable through its own flight");

        Assert.That(await repo.DeleteAsync(flight, mine), Is.True);
        Assert.That(await repo.GetAsync(flight, mine), Is.Null);
        Assert.That(await repo.DeleteAsync(flight, mine), Is.False);
    }
}
