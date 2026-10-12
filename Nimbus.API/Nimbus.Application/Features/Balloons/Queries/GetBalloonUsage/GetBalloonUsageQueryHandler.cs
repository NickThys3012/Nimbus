using MediatR;
using Nimbus.Application.Common.Exceptions;
using Nimbus.Application.Common.Interfaces;
using Nimbus.Application.Common.Ownership;
using Nimbus.Contracts.DTOs.Features.Balloons;
using Nimbus.Contracts.Mappers;
using Nimbus.Domain.Entities;
namespace Nimbus.Application.Features.Balloons.Queries.GetBalloonUsage;

public sealed class GetBalloonUsageQueryHandler : IRequestHandler<GetBalloonUsageQuery, BalloonUsageDto>
{
    private readonly ICurrentUserService _currentUser;
    private readonly IBalloonService _balloons;

    public GetBalloonUsageQueryHandler(IBalloonService balloons, ICurrentUserService currentUser)
    {
        _balloons = balloons;
        _currentUser = currentUser;
    }

    public async Task<BalloonUsageDto> Handle(GetBalloonUsageQuery request, CancellationToken ct)
    {
        var usage = await _balloons.GetUsageAsync(new Caller(_currentUser.UserId, false), request.Id, ct)
                    ?? throw new NotFoundException(nameof(Balloon), request.Id);
        return usage.MapToDto();
    }
}
