using MediatR;
using Nimbus.Application.Common.Interfaces;
using Nimbus.Application.Features.FlightAssessment;
using Nimbus.Contracts.DTOs.Features.FlightPreparations;
using Nimbus.Domain.Interfaces;
namespace Nimbus.Application.Features.FlightPreparations.Queries.ListFlights;

public sealed class ListFlightsQueryHandler : IRequestHandler<ListFlightsQuery, FlightListPageDto>
{
    private readonly IFlightAssessmentService _assessment;
    private readonly ICurrentUserService _currentUser;
    private readonly IUserDirectory _directory;
    private readonly IFlightListReader _flights;

    public ListFlightsQueryHandler(
        IFlightListReader flights, IFlightAssessmentService assessment, IUserDirectory directory, ICurrentUserService currentUser)
    {
        _flights = flights;
        _assessment = assessment;
        _directory = directory;
        _currentUser = currentUser;
    }

    public async Task<FlightListPageDto> Handle(ListFlightsQuery request, CancellationToken ct)
    {
        var me = _currentUser.UserId;
        var page = await _flights.GetPageAsync(
            new FlightListCriteria(me, _currentUser.IsAdmin, request.Filter, request.Sort, request.Descending, request.Page, request.PageSize), ct);

        // The verdict is never recomputed here: one batched call to the assessment service (limits resolved once).
        var assessments = page.Items.Count == 0
            ? []
            : await _assessment.AssessManyAsync(page.Items.Select(i => i.Assessment).ToList(), ct);

        // One lookup for every foreign owner on the page, not one per row.
        var foreignOwners = page.Items.Select(i => i.OwnerId).Where(o => o != me).Distinct().ToList();
        var names = foreignOwners.Count == 0
            ? new Dictionary<Guid, string>()
            : await _directory.GetDisplayNamesAsync(foreignOwners, ct);

        var items = page.Items.Select((row, index) => new FlightListItemDto(
            row.Id, row.Title, row.FlightDate, row.FlightTime, row.BalloonRegistration, row.LocationName,
            row.Status == Domain.Enums.FlightPreparationStatus.Flown, row.FlownAt, assessments[index].GoNoGoVerdict,
            row.OwnerId != me, row.OwnerId == me ? null : names.GetValueOrDefault(row.OwnerId))).ToList();

        return new FlightListPageDto(items, page.TotalCount, request.Page, request.PageSize);
    }
}
