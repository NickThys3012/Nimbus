using MediatR;
using Nimbus.Application.Common.Exceptions;
using Nimbus.Application.Common.Interfaces;
using Nimbus.Application.Common.Ownership;
using Nimbus.Contracts.DTOs.Features.LaunchLocations;
using Nimbus.Contracts.Mappers;
using Nimbus.Domain.Entities;
namespace Nimbus.Application.Features.LaunchLocations.Commands.UpdateLaunchLocation;

public sealed class UpdateLaunchLocationCommandHandler : IRequestHandler<UpdateLaunchLocationCommand, LaunchLocationDto>
{
    private readonly ICurrentUserService _currentUser;
    private readonly ILaunchLocationService _launchLocations;

    public UpdateLaunchLocationCommandHandler(ILaunchLocationService launchLocations, ICurrentUserService currentUser)
    {
        _launchLocations = launchLocations;
        _currentUser = currentUser;
    }

    public async Task<LaunchLocationDto> Handle(UpdateLaunchLocationCommand request, CancellationToken ct)
    {
        var caller = new Caller(_currentUser.UserId, false);
        // A missing and a foreign launch location are indistinguishable: both are 404.
        if (await _launchLocations.UpdateAsync(caller, request.Id, request.LaunchLocation.MapToEntity(), ct) == OwnedResult.NotFound)
        {
            throw new NotFoundException(nameof(LaunchLocation), request.Id);
        }

        var updated = await _launchLocations.GetAsync(caller, request.Id, ct) ?? throw new NotFoundException(nameof(LaunchLocation), request.Id);
        return updated.MapToDto();
    }
}
