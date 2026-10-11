using MediatR;
using Nimbus.Contracts.DTOs.Features.Admin;
using Nimbus.Contracts.Mappers;
using Nimbus.Domain.Interfaces;
namespace Nimbus.Application.Features.Admin.Queries.ListLoginEvents;

public sealed class ListLoginEventsQueryHandler : IRequestHandler<ListLoginEventsQuery, IReadOnlyList<LoginEventDto>>
{
    private readonly ILoginEventRepository _events;

    public ListLoginEventsQueryHandler(ILoginEventRepository events)
    {
        _events = events;
    }

    public async Task<IReadOnlyList<LoginEventDto>> Handle(ListLoginEventsQuery request, CancellationToken ct)
    {
        var events = await _events.ListRecentAsync(request.Limit, ct);
        return events.Select(e => e.MapToLoginEventDto()).ToList();
    }
}
