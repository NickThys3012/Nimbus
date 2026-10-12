using MediatR;
using Nimbus.Application.Common.Interfaces;
using Nimbus.Application.Common.Ownership;
using Nimbus.Contracts.DTOs.Features.Balloons;
using Nimbus.Contracts.Mappers;
namespace Nimbus.Application.Features.Balloons.Queries.ListBalloons;

public sealed class ListBalloonsQueryHandler : IRequestHandler<ListBalloonsQuery, IReadOnlyList<BalloonDto>>
{
    private readonly ICurrentUserService _currentUser;
    private readonly IBalloonService _balloons;

    public ListBalloonsQueryHandler(IBalloonService balloons, ICurrentUserService currentUser)
    {
        _balloons = balloons;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<BalloonDto>> Handle(ListBalloonsQuery request, CancellationToken ct)
    {
        var mine = await _balloons.ListAsync(new Caller(_currentUser.UserId, false), ct);
        return mine.OrderBy(b => b.Registration).Select(b => b.MapToDto()).ToList();
    }
}
