using MediatR;
using Nimbus.Application.Common.Exceptions;
using Nimbus.Application.Common.Interfaces;
using Nimbus.Application.Common.Ownership;
using Nimbus.Domain.Entities;
namespace Nimbus.Application.Features.LaunchLocations.Commands.DeleteLaunchLocation;

public sealed class DeleteLaunchLocationCommandHandler : IRequestHandler<DeleteLaunchLocationCommand>
{
    private readonly ICurrentUserService _currentUser;
    private readonly ILaunchLocationService _launchLocations;

    public DeleteLaunchLocationCommandHandler(ILaunchLocationService launchLocations, ICurrentUserService currentUser)
    {
        _launchLocations = launchLocations;
        _currentUser = currentUser;
    }

    public async Task Handle(DeleteLaunchLocationCommand request, CancellationToken ct)
    {
        if (await _launchLocations.DeleteAsync(new Caller(_currentUser.UserId, false), request.Id, ct) == OwnedResult.NotFound)
        {
            throw new NotFoundException(nameof(LaunchLocation), request.Id);
        }
    }
}
