using Nimbus.Application.Common.Interfaces;
using Nimbus.Application.Features.FlightAssessment;
using Nimbus.Application.Features.FlightPreparations.Queries.ListFlights;
using Nimbus.Contracts.DTOs.Features.FlightAssessment;
using Nimbus.Domain.Entities;
using Nimbus.Domain.Enums;
using Nimbus.Domain.Interfaces;
using Nimbus.Domain.Services;
namespace Nimbus.Application.Tests;

/// <summary>Issue #40: the list reuses the assessment service (one batch call), resolves owner names in one lookup and passes the caller's identity to the reader.</summary>
public class FlightListHandlerTests
{
    private static readonly FlightAssessmentInput Input = new(100, 0, 0, 0, 80, [], 500, null, null, null);

    private static FlightListRow Row(Guid owner, string title) =>
        new(Guid.NewGuid(), title, new DateOnly(2026, 8, 8), new TimeOnly(18, 45), "OO-BLK", "Weelde", FlightPreparationStatus.Planned, null, owner, Input);

    [Test]
    public async Task Handle_UsesOneBatchedAssessmentAndOneNameLookup_AndNeverRecomputesTheVerdict()
    {
        var me = Guid.NewGuid();
        var other = Guid.NewGuid();
        var reader = new Reader(new FlightListPage([Row(me, "mine"), Row(other, "theirs"), Row(other, "theirs 2")], 3));
        var assessment = new Assess();
        var directory = new Directory(new Dictionary<Guid, string> { [other] = "Bob Builder" });
        var handler = new ListFlightsQueryHandler(reader, assessment, directory, new Me(me, false));

        var result = await handler.Handle(new ListFlightsQuery(FlightListFilter.All, FlightListSort.Date, true, 1, 20), default);

        Assert.That(assessment.BatchCalls, Is.EqualTo(1));
        Assert.That(directory.Calls, Is.EqualTo(1));
        Assert.That(directory.Requested, Is.EqualTo(new[] { other }), "the caller's own id is never looked up");
        Assert.That(result.Items.Select(i => i.GoNoGoVerdict), Is.All.EqualTo("sentinel"));
        Assert.That(result.Items.Select(i => i.OwnerName), Is.EqualTo(new[] { null, "Bob Builder", "Bob Builder" }));
        Assert.That(result.Items.Select(i => i.IsShared), Is.EqualTo(new[] { false, true, true }));
        Assert.That(result.TotalCount, Is.EqualTo(3));
    }

    [Test]
    public async Task Handle_TakesUserAndAdminFlagFromTheAuthContext()
    {
        var me = Guid.NewGuid();
        var reader = new Reader(new FlightListPage([], 0));
        var handler = new ListFlightsQueryHandler(reader, new Assess(), new Directory([]), new Me(me, true));

        var result = await handler.Handle(new ListFlightsQuery(FlightListFilter.Flown, FlightListSort.Balloon, false, 2, 10), default);

        Assert.That(reader.Seen, Is.EqualTo(new FlightListCriteria(me, true, FlightListFilter.Flown, FlightListSort.Balloon, false, 2, 10)));
        Assert.That(result.Items, Is.Empty);
    }

    [Test]
    public void Validator_RejectsBadPaging()
    {
        var v = new ListFlightsQueryValidator();
        Assert.That(v.Validate(new ListFlightsQuery(FlightListFilter.All, FlightListSort.Date, true, 1, 20)).IsValid, Is.True);
        Assert.That(v.Validate(new ListFlightsQuery(FlightListFilter.All, FlightListSort.Date, true, 0, 20)).IsValid, Is.False);
        Assert.That(v.Validate(new ListFlightsQuery(FlightListFilter.All, FlightListSort.Date, true, 1, 0)).IsValid, Is.False);
        Assert.That(v.Validate(new ListFlightsQuery(FlightListFilter.All, FlightListSort.Date, true, 1, 101)).IsValid, Is.False);
        Assert.That(v.Validate(new ListFlightsQuery((FlightListFilter)99, FlightListSort.Date, true, 1, 20)).IsValid, Is.False);
    }

    private sealed class Me(Guid id, bool admin) : ICurrentUserService
    {
        public Guid UserId => id;
        public bool IsAdmin => admin;
        public Task<User?> GetCurrentUserAsync() => Task.FromResult<User?>(null);
    }

    private sealed class Reader(FlightListPage page) : IFlightListReader
    {
        public FlightListCriteria? Seen { get; private set; }

        public Task<FlightListPage> GetPageAsync(FlightListCriteria criteria, CancellationToken ct = default)
        {
            Seen = criteria;
            return Task.FromResult(page);
        }
    }

    private sealed class Assess : IFlightAssessmentService
    {
        public int BatchCalls { get; private set; }

        public Task<FlightAssessmentDto> AssessAsync(FlightAssessmentInput input, CancellationToken ct = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<FlightAssessmentDto>> AssessManyAsync(IReadOnlyList<FlightAssessmentInput> inputs, CancellationToken ct = default)
        {
            BatchCalls++;
            return Task.FromResult<IReadOnlyList<FlightAssessmentDto>>(inputs.Select(_ => new FlightAssessmentDto(0, 0, 0, null, "sentinel")).ToList());
        }
    }

    private sealed class Directory(Dictionary<Guid, string> names) : IUserDirectory
    {
        public int Calls { get; private set; }
        public IReadOnlyCollection<Guid> Requested { get; private set; } = [];

        public Task<IReadOnlyDictionary<Guid, string>> GetDisplayNamesAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct = default)
        {
            Calls++;
            Requested = userIds;
            return Task.FromResult<IReadOnlyDictionary<Guid, string>>(names);
        }
    }
}
