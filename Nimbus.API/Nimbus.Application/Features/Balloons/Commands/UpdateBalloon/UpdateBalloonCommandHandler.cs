using MediatR;
using Nimbus.Application.Common.Exceptions;
using Nimbus.Application.Common.Interfaces;
using Nimbus.Application.Common.Ownership;
using Nimbus.Contracts.DTOs.Features.Balloons;
using Nimbus.Contracts.Mappers;
using Nimbus.Domain.Entities;
namespace Nimbus.Application.Features.Balloons.Commands.UpdateBalloon;

public sealed class UpdateBalloonCommandHandler : IRequestHandler<UpdateBalloonCommand, BalloonDto>
{
    private readonly ICurrentUserService _currentUser;
    private readonly IBalloonService _balloons;

    public UpdateBalloonCommandHandler(IBalloonService balloons, ICurrentUserService currentUser)
    {
        _balloons = balloons;
        _currentUser = currentUser;
    }

    public async Task<BalloonDto> Handle(UpdateBalloonCommand request, CancellationToken ct)
    {
        var caller = new Caller(_currentUser.UserId, false);
        // A missing and a foreign balloon are indistinguishable: both are 404.
        if (await _balloons.UpdateAsync(caller, request.Id, request.Balloon.MapToEntity(), ct) == OwnedResult.NotFound)
        {
            throw new NotFoundException(nameof(Balloon), request.Id);
        }

        var updated = await _balloons.GetAsync(caller, request.Id, ct) ?? throw new NotFoundException(nameof(Balloon), request.Id);
        return updated.MapToDto();
    }
}
