using Nimbus.Application.Common;
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
        var created = await new CreateFlightPreparationCommandHandler(repo, new Me(me), new RichTextSanitizer()).Handle(new CreateFlightPreparationCommand(Req()), default);

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
        var result = await new UpdateFlightPreparationCommandHandler(repo, new Access(repo), new Me(me), new RichTextSanitizer()).Handle(new UpdateFlightPreparationCommand(stored.Id, edited), default);

        Assert.That(result.Status, Is.EqualTo("Flown"));
        Assert.That(repo.Rows.Single().OutcomeRemarks, Is.EqualTo("smooth"));
        Assert.That(result.Details.Passengers.Select(p => p.Name), Is.EqualTo(new[] { "Carol" }));
    }

    [Test]
    public void Update_OfAnotherUsersFlight_IsForbidden_AndOfAMissingOne_NotFound()
    {
        var repo = new Repo();
        var theirs = Req().ApplyTo(new FlightPreparation { Id = Guid.NewGuid(), OwnerId = Guid.NewGuid() });
        repo.Rows.Add(theirs);
        var handler = new UpdateFlightPreparationCommandHandler(repo, new Access(repo), new Me(Guid.NewGuid()), new RichTextSanitizer());

        Assert.ThrowsAsync<ForbiddenException>(() => handler.Handle(new UpdateFlightPreparationCommand(theirs.Id, Req()), default));
        Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new UpdateFlightPreparationCommand(Guid.NewGuid(), Req()), default));
    }

    [Test]
    public async Task Update_ByAnAdmin_EditsSomeoneElsesFlight_OnTheOwnersBehalf()
    {
        var owner = Guid.NewGuid();
        var repo = new Repo();
        var stored = Req().ApplyTo(new FlightPreparation { Id = Guid.NewGuid(), OwnerId = owner, Status = FlightPreparationStatus.Flown, OutcomeRemarks = "smooth" });
        repo.Rows.Add(stored);
        var admin = new Me(Guid.NewGuid(), admin: true);

        var result = await new UpdateFlightPreparationCommandHandler(repo, new Access(repo), admin, new RichTextSanitizer())
            .Handle(new UpdateFlightPreparationCommand(stored.Id, Req() with { Title = "Edited by admin" }), default);

        Assert.That(repo.LastUpdateAskedAs, Is.EqualTo(owner), "the owner-scoped repository is asked as the flight's owner");
        Assert.That(result.Status, Is.EqualTo("Flown"));
        Assert.That(repo.Rows.Single().OwnerId, Is.EqualTo(owner), "ownership never moves to the admin");
        Assert.That(repo.Rows.Single().Title, Is.EqualTo("Edited by admin"));
        Assert.That(repo.Rows.Single().OutcomeRemarks, Is.EqualTo("smooth"));
    }

    [Test]
    public async Task Get_ForTheForm_ReturnsTheOwnersFlight()
    {
        var me = Guid.NewGuid();
        var repo = new Repo();
        var mine = Req().ApplyTo(new FlightPreparation { Id = Guid.NewGuid(), OwnerId = me });
        repo.Rows.Add(mine);

        var dto = await new GetFlightPreparationQueryHandler(repo, new Access(repo), new Me(me)).Handle(new GetFlightPreparationQuery(mine.Id), default);

        Assert.That(dto.Id, Is.EqualTo(mine.Id));
    }

    [Test]
    public async Task Get_ForTheForm_ByAnAdmin_ReturnsSomeoneElsesFlight()
    {
        var repo = new Repo();
        var theirs = Req().ApplyTo(new FlightPreparation { Id = Guid.NewGuid(), OwnerId = Guid.NewGuid() });
        repo.Rows.Add(theirs);

        var dto = await new GetFlightPreparationQueryHandler(repo, new Access(repo), new Me(Guid.NewGuid(), admin: true)).Handle(new GetFlightPreparationQuery(theirs.Id), default);

        Assert.That(dto.Id, Is.EqualTo(theirs.Id));
    }

    [Test]
    public void Get_ForTheForm_OfAnotherUsersFlight_IsForbidden_AndOfAMissingOne_NotFound()
    {
        var repo = new Repo();
        var theirs = Req().ApplyTo(new FlightPreparation { Id = Guid.NewGuid(), OwnerId = Guid.NewGuid() });
        repo.Rows.Add(theirs);
        var handler = new GetFlightPreparationQueryHandler(repo, new Access(repo), new Me(Guid.NewGuid()));

        Assert.ThrowsAsync<ForbiddenException>(() => handler.Handle(new GetFlightPreparationQuery(theirs.Id), default));
        Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new GetFlightPreparationQuery(Guid.NewGuid()), default));
    }

    private const string Hostile = "<p>Hold on</p><script>alert(1)</script><img src=x onerror=alert(1)>";

    [Test]
    public async Task Create_StoresTheBriefingSanitised()
    {
        var repo = new Repo();
        await new CreateFlightPreparationCommandHandler(repo, new Me(Guid.NewGuid()), new RichTextSanitizer())
            .Handle(new CreateFlightPreparationCommand(Req() with { PaxBriefingText = Hostile }), default);

        Assert.That(repo.Rows.Single().PaxBriefingText, Is.EqualTo("<p>Hold on</p>"));
    }

    [Test]
    public async Task Update_StoresTheBriefingSanitised_AndAnEmptyEditorBecomesNull()
    {
        var me = Guid.NewGuid();
        var repo = new Repo();
        var stored = Req().ApplyTo(new FlightPreparation { Id = Guid.NewGuid(), OwnerId = me });
        repo.Rows.Add(stored);
        var handler = new UpdateFlightPreparationCommandHandler(repo, new Access(repo), new Me(me), new RichTextSanitizer());

        var hostile = await handler.Handle(new UpdateFlightPreparationCommand(stored.Id, Req() with { PaxBriefingText = Hostile }), default);
        Assert.That(hostile.Details.PaxBriefingText, Is.EqualTo("<p>Hold on</p>"));

        var empty = await handler.Handle(new UpdateFlightPreparationCommand(stored.Id, Req() with { PaxBriefingText = "<p><br></p>" }), default);
        Assert.That(empty.Details.PaxBriefingText, Is.Null);
    }

    [Test]
    public void Validator_RejectsAnOversizedBriefing()
    {
        var v = new FlightPreparationRequestValidator();
        Assert.That(v.Validate(Req() with { PaxBriefingText = new string('x', FlightPreparationRequestValidator.MaxBriefingLength) }).IsValid, Is.True);
        Assert.That(v.Validate(Req() with { PaxBriefingText = new string('x', FlightPreparationRequestValidator.MaxBriefingLength + 1) }).IsValid, Is.False);
    }

    [Test]
    public void Validator_AcceptsAWholeFetchedMetarAndTaf_AndRefusesOnesThatCannotBeStored()
    {
        var v = new FlightPreparationRequestValidator();
        Assert.That(v.Validate(Req() with { Metar = new string('m', 1000), Taf = new string('t', 2000) }).IsValid, Is.True);
        Assert.That(v.Validate(Req() with { Metar = new string('m', 1001) }).IsValid, Is.False);
        Assert.That(v.Validate(Req() with { Taf = new string('t', 2001) }).IsValid, Is.False);
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

    private sealed class Me(Guid id, bool admin = false) : ICurrentUserService
    {
        public Guid UserId => id;
        public bool IsAdmin => admin;
        public Task<User?> GetCurrentUserAsync() => Task.FromResult<User?>(null);
    }

    /// <summary>The access answer the real reader gives: the owner is the owner, everyone else may not edit.</summary>
    private sealed class Access(Repo repo) : IFlightDetailReader
    {
        public Task<FlightAccess> GetAccessAsync(Guid viewerId, bool isAdmin, Guid flightId, CancellationToken ct = default)
        {
            var flight = repo.Rows.FirstOrDefault(f => f.Id == flightId);
            return Task.FromResult(flight is null
                ? new FlightAccess(false, null)
                : new FlightAccess(true, flight.OwnerId == viewerId ? FlightViewerRole.Owner : isAdmin ? FlightViewerRole.Admin : null, flight.OwnerId));
        }

        public Task<FlightDetailResult> GetAsync(Guid viewerId, bool isAdmin, Guid flightId, CancellationToken ct = default) => throw new NotSupportedException();
    }

    /// <summary>Owner-scoped like the real repository: another user's flight behaves like a missing one.</summary>
    private sealed class Repo : IFlightPreparationRepository
    {
        public List<FlightPreparation> Rows { get; } = [];

        /// <summary>The owner id the last update was asked as.</summary>
        public Guid? LastUpdateAskedAs { get; private set; }

        public Task<FlightPreparation?> GetByIdAsync(Guid ownerId, Guid id, CancellationToken ct = default) =>
            Task.FromResult(Rows.FirstOrDefault(f => f.Id == id && f.OwnerId == ownerId));

        public Task AddAsync(FlightPreparation flight, CancellationToken ct = default)
        {
            flight.Id = Guid.NewGuid();
            Rows.Add(flight);
            return Task.CompletedTask;
        }

        public Task<bool> UpdateAsync(Guid ownerId, FlightPreparation flight, CancellationToken ct = default)
        {
            LastUpdateAskedAs = ownerId;
            return Task.FromResult(Rows.Any(f => f.Id == flight.Id && f.OwnerId == ownerId));
        }

        public Task<FlightDeletion?> DeleteAsync(Guid ownerId, Guid id, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<bool> SetTrajectoryJsonAsync(Guid ownerId, Guid id, string? trajectoryJson, CancellationToken ct = default) => Task.FromResult(false);

        public Task<bool> SetKmlTrackAsync(Guid ownerId, Guid id, string? objectKey, string? contentType, CancellationToken ct = default) => Task.FromResult(false);
    }
}
