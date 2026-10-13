using MediatR;
using Nimbus.Application.Common.Exceptions;
using Nimbus.Application.Common.Interfaces;
using Nimbus.Application.Common.Ownership;
using Nimbus.Contracts.DTOs.Features.LaunchLocations;
using Nimbus.Contracts.Mappers;
using Nimbus.Domain.Entities;
namespace Nimbus.Application.Features.LaunchLocations.Queries.GetLaunchLocation;

public sealed class GetLaunchLocationQueryHandler : IRequestHandler<GetLaunchLocationQuery, LaunchLocationDto>
{
    private readonly ICurrentUserService _currentUser;
    private readonly ILaunchLocationService _launchLocations;

    public GetLaunchLocationQueryHandler(ILaunchLocationService launchLocations, ICurrentUserService currentUser)
    {
        _launchLocations = launchLocations;
        _currentUser = currentUser;
    }

    public async Task<LaunchLocationDto> Handle(GetLaunchLocationQuery request, CancellationToken ct)
    {
        var launchLocation = await _launchLocations.GetAsync(new Caller(_currentUser.UserId, false), request.Id, ct)
                    ?? throw new NotFoundException(nameof(LaunchLocation), request.Id);
        return launchLocation.MapToDto();
    }
}
