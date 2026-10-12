using MediatR;
using Nimbus.Application.Common.Exceptions;
using Nimbus.Application.Common.Interfaces;
using Nimbus.Application.Common.Ownership;
using Nimbus.Contracts.DTOs.Features.Balloons;
using Nimbus.Contracts.Mappers;
using Nimbus.Domain.Entities;
namespace Nimbus.Application.Features.Balloons.Queries.GetBalloon;

public sealed class GetBalloonQueryHandler : IRequestHandler<GetBalloonQuery, BalloonDto>
{
    private readonly ICurrentUserService _currentUser;
    private readonly IBalloonService _balloons;

    public GetBalloonQueryHandler(IBalloonService balloons, ICurrentUserService currentUser)
    {
        _balloons = balloons;
        _currentUser = currentUser;
    }

    public async Task<BalloonDto> Handle(GetBalloonQuery request, CancellationToken ct)
    {
        var balloon = await _balloons.GetAsync(new Caller(_currentUser.UserId, false), request.Id, ct)
                      ?? throw new NotFoundException(nameof(Balloon), request.Id);
        return balloon.MapToDto();
    }
}
