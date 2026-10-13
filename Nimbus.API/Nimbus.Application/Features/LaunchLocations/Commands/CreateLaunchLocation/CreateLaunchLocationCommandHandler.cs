using MediatR;
using Nimbus.Application.Common.Interfaces;
using Nimbus.Application.Common.Ownership;
using Nimbus.Contracts.DTOs.Features.LaunchLocations;
using Nimbus.Contracts.Mappers;
namespace Nimbus.Application.Features.LaunchLocations.Commands.CreateLaunchLocation;

public sealed class CreateLaunchLocationCommandHandler : IRequestHandler<CreateLaunchLocationCommand, LaunchLocationDto>
{
    private readonly ICurrentUserService _currentUser;
    private readonly ILaunchLocationService _launchLocations;

    public CreateLaunchLocationCommandHandler(ILaunchLocationService launchLocations, ICurrentUserService currentUser)
    {
        _launchLocations = launchLocations;
        _currentUser = currentUser;
    }

    public async Task<LaunchLocationDto> Handle(CreateLaunchLocationCommand request, CancellationToken ct)
    {
        // "My launch locations" is always the caller's own, even for an admin.
        var created = await _launchLocations.CreateAsync(new Caller(_currentUser.UserId, false), request.LaunchLocation.MapToEntity(), ct);
        return created.MapToDto();
    }
}
