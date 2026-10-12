using MediatR;
using Nimbus.Contracts.DTOs.Features.Balloons;
namespace Nimbus.Application.Features.Balloons.Queries.GetBalloon;

public sealed record GetBalloonQuery(Guid Id) : IRequest<BalloonDto>;
