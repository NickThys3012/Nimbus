using Nimbus.Application.Common.Exceptions;
using Nimbus.Application.Common.Interfaces;
using Nimbus.Application.Features.FlightPreparations;
using Nimbus.Application.Features.FlightPreparations.Commands.CreateFlightPreparation;
using Nimbus.Application.Features.FlightPreparations.Commands.UpdateFlightPreparation;
using Nimbus.Application.Features.FlightPreparations.Queries.GetFlightPreparation;
using Nimbus.Contracts.DTOs.Features.FlightPreparations;
using Nimbus.Contracts.Mappers;
using Nimbus.Domain.Entities;
using Nimbus.Domain.Enums;
using Nimbus.Domain.Interfaces;
namespace Nimbus.Application.Tests;

/// <summary>Saving and loading the dossier: owner scoping, form-owned vs. untouched columns, mapping and field rules.</summary>
public class FlightPreparationSaveTests
{
    private static FlightPreparationRequestDto Req() => new()
    {
        Title = "  Weelde · avondvlucht ",
        BalloonId = Guid.NewGuid(),
        PilotId = Guid.NewGuid(),
        LaunchLocationId = Guid.NewGuid(),
        FlightDate = new DateOnly(2026, 8, 8),
        FlightTime = new TimeOnly(18, 45),
        WindSpeedKt = 6.5m,
        BasketWeightKg = 90m,
        BurnerWeightKg = 25m,
        CylindersWeightKg = 60m,
        WindDirectionDeg = 270,
        Metar = "  EBBR 081650Z  ",
        Taf = "   ",
        FirstAidAndExtinguisher = ChecklistAnswer.Yes,
        WindLevels = [new WindLevelDto(100, 90, 5, 12), new WindLevelDto(300, 120, 9, 8)],
        Passengers = [new PassengerDto(" Alice ", 62, false, false, true), new PassengerDto("Bob", 80, true, false, false)]
    };

    [Test]
    public void Mapper_TrimsText_BlankBecomesNull_AndKeepsListOrder()
    {
        var f = Req().ApplyTo(new FlightPreparation());
        Assert.That(f.Title, Is.EqualTo("Weelde · avondvlucht"));
        Assert.That(f.Metar, Is.EqualTo("EBBR 081650Z"));
        Assert.That(f.Taf, Is.Null);
        Assert.That(f.Passengers.Select(p => p.Name), Is.EqualTo(new[] { "Alice", "Bob" }));
        Assert.That(f.WindLevels.Select(w => w.AltitudeM), Is.EqualTo(new[] { 100, 300 }));
    }

    [Test]
    public void Mapper_BlankTitleFallsBackToTheFlightDate()
    {
        var r = Req() with { Title = " " };
        Assert.That(r.ApplyTo(new FlightPreparation()).Title, Is.EqualTo("Flight 2026-08-08"));
    }

    [Test]
    public void Mapper_ApplyThenMapBack_RoundTripsTheFormOwnedFields()
    {
        var request = Req();
        var back = request.ApplyTo(new FlightPreparation()).MapToRequestDto();
        Assert.That(back.BalloonId, Is.EqualTo(request.BalloonId));
        Assert.That(back.WindSpeedKt, Is.EqualTo(6.5m));
        Assert.That((back.BasketWeightKg, back.BurnerWeightKg, back.CylindersWeightKg), Is.EqualTo((90m, 25m, 60m)));
        Assert.That(back.FirstAidAndExtinguisher, Is.EqualTo(ChecklistAnswer.Yes));
        Assert.That(back.Passengers.Select(p => p.Name), Is.EqualTo(new[] { "Alice", "Bob" }));
        Assert.That(back.Passengers[0].NeedsTransport, Is.True);
    }

    [Test]
    public void Mapper_ApplyingARequest_NeverTouchesColumnsTheFormDoesNotOwn()
    {
        var flownAt = new DateTime(2026, 8, 8, 20, 0, 0, DateTimeKind.Utc);
        var stored = new FlightPreparation
        {
            Status = FlightPreparationStatus.Flown, FlownAt = flownAt, OutcomeRemarks = "smooth", Precipitation = "none",
            SimulationDurationMinutes = 90, TrajectoryJson = "{}", KmlTrackObjectKey = "k"
        };
        Req().ApplyTo(stored);
        Assert.That(stored.Status, Is.EqualTo(FlightPreparationStatus.Flown));
        Assert.That(stored.FlownAt, Is.EqualTo(flownAt));
        Assert.That(stored.OutcomeRemarks, Is.EqualTo("smooth"));
        Assert.That(stored.Precipitation, Is.EqualTo("none"));
        Assert.That(stored.SimulationDurationMinutes, Is.EqualTo(90));
        Assert.That(stored.TrajectoryJson, Is.EqualTo("{}"));
        Assert.That(stored.KmlTrackObjectKey, Is.EqualTo("k"));
    }

    [Test]
    public async Task Create_MakesTheCallerTheOwner_AndTheFlightPlanned()
    {
        var me = Guid.NewGuid();
        var repo = new Repo();
        var created = await new CreateFlightPreparationCommandHandler(repo, new Me(me)).Handle(new CreateFlightPreparationCommand(Req()), default);

        Assert.That(repo.Rows.Single().OwnerId, Is.EqualTo(me));
        Assert.That(created.Status, Is.EqualTo("Planned"));
        Assert.That(created.Id, Is.EqualTo(repo.Rows.Single().Id));
    }

    [Test]
    public async Task Update_KeepsOutcomeColumns_AndReplacesTheLists()
    {
        var me = Guid.NewGuid();
        var repo = new Repo();
        var stored = Req().ApplyTo(new FlightPreparation { Id = Guid.NewGuid(), OwnerId = me, Status = FlightPreparationStatus.Flown, OutcomeRemarks = "smooth" });
        repo.Rows.Add(stored);

        var edited = Req() with { Passengers = [new PassengerDto("Carol", 55, false, false, false)] };
        var result = await new UpdateFlightPreparationCommandHandler(repo, new Me(me)).Handle(new UpdateFlightPreparationCommand(stored.Id, edited), default);

        Assert.That(result.Status, Is.EqualTo("Flown"));
        Assert.That(repo.Rows.Single().OutcomeRemarks, Is.EqualTo("smooth"));
        Assert.That(result.Details.Passengers.Select(p => p.Name), Is.EqualTo(new[] { "Carol" }));
    }

    [Test]
    public void Update_AndGet_OfAnotherUsersOrMissingFlight_AreNotFound()
    {
        var repo = new Repo();
        var theirs = Req().ApplyTo(new FlightPreparation { Id = Guid.NewGuid(), OwnerId = Guid.NewGuid() });
        repo.Rows.Add(theirs);
        var me = new Me(Guid.NewGuid());

        Assert.ThrowsAsync<NotFoundException>(() => new UpdateFlightPreparationCommandHandler(repo, me).Handle(new UpdateFlightPreparationCommand(theirs.Id, Req()), default));
        Assert.ThrowsAsync<NotFoundException>(() => new GetFlightPreparationQueryHandler(repo, me).Handle(new GetFlightPreparationQuery(theirs.Id), default));
        Assert.ThrowsAsync<NotFoundException>(() => new GetFlightPreparationQueryHandler(repo, me).Handle(new GetFlightPreparationQuery(Guid.NewGuid()), default));
    }

    [Test]
    public void Validator_AcceptsAValidRequest()
    {
        Assert.That(new FlightPreparationRequestValidator().Validate(Req()).IsValid, Is.True);
    }

    [Test]
    public void Validator_RejectsOutOfRangeAndOversizedValues()
    {
        var v = new FlightPreparationRequestValidator();
        Assert.That(v.Validate(Req() with { WindDirectionDeg = 361 }).IsValid, Is.False);
        Assert.That(v.Validate(Req() with { WindSpeedKt = -1 }).IsValid, Is.False);
        Assert.That(v.Validate(Req() with { BalloonId = Guid.Empty }).IsValid, Is.False);
        Assert.That(v.Validate(Req() with { Title = new string('x', 201) }).IsValid, Is.False);
        Assert.That(v.Validate(Req() with { FirstAidAndExtinguisher = (ChecklistAnswer)9 }).IsValid, Is.False);
        Assert.That(v.Validate(Req() with { Passengers = [new PassengerDto("", 70, false, false, false)] }).IsValid, Is.False);
        Assert.That(v.Validate(Req() with { Passengers = [new PassengerDto("A", 0, false, false, false)] }).IsValid, Is.False);
        Assert.That(v.Validate(Req() with { Passengers = Enumerable.Range(0, 31).Select(i => new PassengerDto($"P{i}", 70, false, false, false)).ToList() }).IsValid, Is.False);
    }

    private sealed class Me(Guid id) : ICurrentUserService
    {
        public Guid UserId => id;
        public Task<User?> GetCurrentUserAsync() => Task.FromResult<User?>(null);
    }

    /// <summary>Owner-scoped like the real repository: another user's flight behaves like a missing one.</summary>
    private sealed class Repo : IFlightPreparationRepository
    {
        public List<FlightPreparation> Rows { get; } = [];

        public Task<FlightPreparation?> GetByIdAsync(Guid ownerId, Guid id, CancellationToken ct = default) =>
            Task.FromResult(Rows.FirstOrDefault(f => f.Id == id && f.OwnerId == ownerId));

        public Task AddAsync(FlightPreparation flight, CancellationToken ct = default)
        {
            flight.Id = Guid.NewGuid();
            Rows.Add(flight);
            return Task.CompletedTask;
        }

        public Task<bool> UpdateAsync(Guid ownerId, FlightPreparation flight, CancellationToken ct = default) =>
            Task.FromResult(Rows.Any(f => f.Id == flight.Id && f.OwnerId == ownerId));

        public Task<bool> SetTrajectoryJsonAsync(Guid ownerId, Guid id, string? trajectoryJson, CancellationToken ct = default) => Task.FromResult(false);

        public Task<bool> SetKmlTrackAsync(Guid ownerId, Guid id, string? objectKey, string? contentType, CancellationToken ct = default) => Task.FromResult(false);
    }
}
