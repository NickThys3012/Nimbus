using MediatR;
using Nimbus.Application.Common.Interfaces;
using Nimbus.Application.Common.Ownership;
using Nimbus.Contracts.DTOs.Features.LaunchLocations;
using Nimbus.Contracts.Mappers;
namespace Nimbus.Application.Features.LaunchLocations.Queries.ListLaunchLocations;

public sealed class ListLaunchLocationsQueryHandler : IRequestHandler<ListLaunchLocationsQuery, IReadOnlyList<LaunchLocationDto>>
{
    private readonly ICurrentUserService _currentUser;
    private readonly ILaunchLocationService _launchLocations;

    public ListLaunchLocationsQueryHandler(ILaunchLocationService launchLocations, ICurrentUserService currentUser)
    {
        _launchLocations = launchLocations;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<LaunchLocationDto>> Handle(ListLaunchLocationsQuery request, CancellationToken ct)
    {
        var mine = await _launchLocations.ListAsync(new Caller(_currentUser.UserId, false), ct);
        // Stable order for the flight-preparation picker: name, then id as a tie-break.
        return mine.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ThenBy(p => p.Id).Select(p => p.MapToDto()).ToList();
    }
}
