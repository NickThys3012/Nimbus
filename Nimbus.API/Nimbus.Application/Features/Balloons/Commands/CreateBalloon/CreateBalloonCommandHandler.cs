using MediatR;
using Nimbus.Application.Common.Interfaces;
using Nimbus.Application.Common.Ownership;
using Nimbus.Contracts.DTOs.Features.Balloons;
using Nimbus.Contracts.Mappers;
namespace Nimbus.Application.Features.Balloons.Commands.CreateBalloon;

public sealed class CreateBalloonCommandHandler : IRequestHandler<CreateBalloonCommand, BalloonDto>
{
    private readonly ICurrentUserService _currentUser;
    private readonly IBalloonService _balloons;

    public CreateBalloonCommandHandler(IBalloonService balloons, ICurrentUserService currentUser)
    {
        _balloons = balloons;
        _currentUser = currentUser;
    }

    public async Task<BalloonDto> Handle(CreateBalloonCommand request, CancellationToken ct)
    {
        // "My balloons" is always the caller's own, even for an admin.
        var created = await _balloons.CreateAsync(new Caller(_currentUser.UserId, false), request.Balloon.MapToEntity(), ct);
        return created.MapToDto();
    }
}
