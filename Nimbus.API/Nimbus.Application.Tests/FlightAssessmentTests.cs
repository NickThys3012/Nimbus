using Nimbus.Application.Common.Interfaces;
using Nimbus.Application.Features.FlightAssessment;
using Nimbus.Application.Features.FlightAssessment.Queries.AssessFlight;
using Nimbus.Contracts.DTOs.Features.FlightAssessment;
using Nimbus.Domain.Entities;
using Nimbus.Domain.Enums;
using Nimbus.Domain.Interfaces;
using Nimbus.Domain.Services;
namespace Nimbus.Application.Tests;

/// <summary>Issue #39: one assessment, identical through every consumer; per-user thresholds; no duplicate verdict on the model.</summary>
public class FlightAssessmentTests
{
    private static FlightPreparation Flight() => new()
    {
        EnvelopeWeightKg = 100m,
        TotalLiftKg = 500m,
        WindSpeedKt = 12m,
        VisibilityKm = 8m,
        CapeJPerKg = 100m,
        Passengers =
        [
            new Passenger { Order = 1, WeightKg = 60m },
            new Passenger { Order = 0, WeightKg = 70m }
        ]
    };

    private static FlightAssessmentRequestDto Request(FlightAssessmentInput i) => new(
        i.EnvelopeWeightKg, i.BasketWeightKg, i.BurnerWeightKg, i.CylindersWeightKg, i.PilotWeightKg,
        i.PassengerWeightsKg.ToList(), i.TotalLiftKg, i.WindSpeedKt, i.VisibilityKm, i.CapeJPerKg);

    [Test]
    public void From_PrefersTheFlightsOwnWeightSnapshot_OverTheFallbackArguments()
    {
        var flight = Flight();
        (flight.BasketWeightKg, flight.BurnerWeightKg, flight.CylindersWeightKg) = (60m, 25m, 35m);

        var input = FlightAssessmentInput.From(flight, pilotWeightKg: 80m, basketWeightKg: 50m, burnerWeightKg: 20m, cylindersWeightKg: 30m);

        Assert.That((input.BasketWeightKg, input.BurnerWeightKg, input.CylindersWeightKg), Is.EqualTo((60m, 25m, 35m)));
        flight.BasketWeightKg = null;
        Assert.That(FlightAssessmentInput.From(flight, 80m, basketWeightKg: 50m).BasketWeightKg, Is.EqualTo(50m));
    }

    [Test]
    public async Task SameFlight_YieldsIdenticalAssessment_ThroughEveryConsumer()
    {
        var me = Guid.NewGuid();
        var service = new FlightAssessmentService(new FakeRepo(), new FakeUser(me));
        var input = FlightAssessmentInput.From(Flight(), pilotWeightKg: 80m, basketWeightKg: 50m, burnerWeightKg: 20m, cylindersWeightKg: 30m);

        // Edit page: unsaved values through the endpoint's query handler.
        var edit = await new AssessFlightQueryHandler(service).Handle(new AssessFlightQuery(Request(input)), default);
        // View page and PDF exporter: the service for one persisted flight.
        var view = await service.AssessAsync(input);
        var pdf = await service.AssessAsync(input);
        // Flight list: batch call.
        var list = (await service.AssessManyAsync([input, input]))[1];

        Assert.That(edit, Is.EqualTo(new FlightAssessmentDto(410m, 130m, 90m, true, "yellow")));
        Assert.That(view, Is.EqualTo(edit));
        Assert.That(pdf, Is.EqualTo(edit));
        Assert.That(list, Is.EqualTo(edit));
    }

    [Test]
    public async Task LiftNotEntered_LiftSufficientIsNull_AndNoMeteoIsUnknown()
    {
        var service = new FlightAssessmentService(new FakeRepo(), new FakeUser(Guid.NewGuid()));
        var input = new FlightAssessmentInput(100, 0, 0, 0, 80, [], 0, null, null, null);
        var dto = await service.AssessAsync(input);
        Assert.That(dto.LiftSufficient, Is.Null);
        Assert.That(dto.GoNoGoVerdict, Is.EqualTo("unknown"));
        Assert.That(dto.TotalWeightKg, Is.EqualTo(180m));
    }

    [Test]
    public async Task Overloaded_IsNotSufficient()
    {
        var service = new FlightAssessmentService(new FakeRepo(), new FakeUser(Guid.NewGuid()));
        var dto = await service.AssessAsync(new FlightAssessmentInput(100, 0, 0, 0, 80, [70m], 150m, 1, 20, 0));
        Assert.That(dto.LiftSufficient, Is.False);
        Assert.That(dto.MarginKg, Is.EqualTo(-100m));
        Assert.That(dto.GoNoGoVerdict, Is.EqualTo("green"));
    }

    [Test]
    public async Task Verdict_UsesOnlyTheCallersOwnThresholds()
    {
        var repo = new FakeRepo();
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        repo.Rows.Add(new GoNoGoThreshold { Id = Guid.NewGuid(), OwnerId = a, WindYellowKt = 30m, WindRedKt = 40m, VisibilityYellowKm = 2m, VisibilityRedKm = 1m, CapeYellow = 900m, CapeRed = 1000m });
        var input = new FlightAssessmentInput(0, 0, 0, 0, 0, [], 0, 12m, 8m, 100m);

        var forA = await new FlightAssessmentService(repo, new FakeUser(a)).AssessAsync(input);
        var forB = await new FlightAssessmentService(repo, new FakeUser(b)).AssessAsync(input);

        Assert.That(forA.GoNoGoVerdict, Is.EqualTo("green"));
        Assert.That(forB.GoNoGoVerdict, Is.EqualTo("yellow")); // defaults: wind yellow at 10 kt
    }

    [Test]
    public void FlightPreparation_HasNoStoredVerdictOrDerivedAssessmentProperty()
    {
        var names = typeof(FlightPreparation).GetProperties().Select(p => p.Name).ToList();
        Assert.That(names, Has.None.Matches<string>(n =>
            n.Contains("GoNoGo", StringComparison.OrdinalIgnoreCase) || n.Contains("Verdict", StringComparison.OrdinalIgnoreCase)
            || n.Contains("TotalWeight", StringComparison.OrdinalIgnoreCase) || n.Contains("LiftSufficient", StringComparison.OrdinalIgnoreCase)));
    }

    [Test]
    public void Validator_RejectsNegativeWeightsAndTooManyPassengers()
    {
        var v = new AssessFlightQueryValidator();
        var ok = Request(new FlightAssessmentInput(1, 0, 0, 0, 80, [70m], 100, null, null, null));
        Assert.That(v.Validate(new AssessFlightQuery(ok)).IsValid, Is.True);
        Assert.That(v.Validate(new AssessFlightQuery(ok with { PilotWeightKg = -1 })).IsValid, Is.False);
        Assert.That(v.Validate(new AssessFlightQuery(ok with { PassengerWeightsKg = Enumerable.Repeat(70m, 51).ToList() })).IsValid, Is.False);
    }

    private sealed class FakeUser(Guid id) : ICurrentUserService
    {
        public Guid UserId => id;
        public Task<User?> GetCurrentUserAsync() => Task.FromResult<User?>(null);
    }

    private sealed class FakeRepo : IGoNoGoThresholdsRepository
    {
        public List<GoNoGoThreshold> Rows { get; } = [];

        public Task<GoNoGoThreshold?> GetByOwnerAsync(Guid ownerId, CancellationToken ct = default) =>
            Task.FromResult(Rows.FirstOrDefault(r => r.OwnerId == ownerId));

        public Task<GoNoGoThreshold?> GetGlobalDefaultAsync(CancellationToken ct = default) =>
            Task.FromResult(Rows.FirstOrDefault(r => r.OwnerId is null));

        public Task AddAsync(GoNoGoThreshold e, CancellationToken ct = default) { Rows.Add(e); return Task.CompletedTask; }
        public Task UpdateAsync(GoNoGoThreshold e, CancellationToken ct = default) => Task.CompletedTask;
        public Task RemoveAsync(GoNoGoThreshold e, CancellationToken ct = default) { Rows.Remove(e); return Task.CompletedTask; }
    }
}
