using MediatR;
using Nimbus.Application.Common.Exceptions;
using Nimbus.Application.Common.Interfaces;
using Nimbus.Application.Common.Ownership;
using Nimbus.Domain.Entities;
namespace Nimbus.Application.Features.Balloons.Commands.DeleteBalloon;

public sealed class DeleteBalloonCommandHandler : IRequestHandler<DeleteBalloonCommand>
{
    private readonly ICurrentUserService _currentUser;
    private readonly IBalloonService _balloons;

    public DeleteBalloonCommandHandler(IBalloonService balloons, ICurrentUserService currentUser)
    {
        _balloons = balloons;
        _currentUser = currentUser;
    }

    public async Task Handle(DeleteBalloonCommand request, CancellationToken ct)
    {
        if (await _balloons.DeleteAsync(new Caller(_currentUser.UserId, false), request.Id, ct) == OwnedResult.NotFound)
        {
            throw new NotFoundException(nameof(Balloon), request.Id);
        }
    }
}
