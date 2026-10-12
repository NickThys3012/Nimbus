using MediatR;
using Nimbus.Contracts.DTOs.Features.Balloons;
namespace Nimbus.Application.Features.Balloons.Queries.GetBalloonUsage;

public sealed record GetBalloonUsageQuery(Guid Id) : IRequest<BalloonUsageDto>;
