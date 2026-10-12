using MediatR;
using Nimbus.Contracts.DTOs.Features.Balloons;
namespace Nimbus.Application.Features.Balloons.Queries.ListBalloons;

public sealed record ListBalloonsQuery : IRequest<IReadOnlyList<BalloonDto>>;
