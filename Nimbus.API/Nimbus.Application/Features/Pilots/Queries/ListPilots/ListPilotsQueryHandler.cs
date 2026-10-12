using MediatR;
using Nimbus.Application.Common.Interfaces;
using Nimbus.Application.Common.Ownership;
using Nimbus.Contracts.DTOs.Features.Pilots;
using Nimbus.Contracts.Mappers;
namespace Nimbus.Application.Features.Pilots.Queries.ListPilots;

public sealed class ListPilotsQueryHandler : IRequestHandler<ListPilotsQuery, IReadOnlyList<PilotDto>>
{
    private readonly ICurrentUserService _currentUser;
    private readonly IPilotService _pilots;

    public ListPilotsQueryHandler(IPilotService pilots, ICurrentUserService currentUser)
    {
        _pilots = pilots;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<PilotDto>> Handle(ListPilotsQuery request, CancellationToken ct)
    {
        var mine = await _pilots.ListAsync(new Caller(_currentUser.UserId, false), ct);
        // Stable order for the flight-preparation picker: name, then id as a tie-break.
        return mine.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ThenBy(p => p.Id).Select(p => p.MapToDto()).ToList();
    }
}
