using MediatR;
using Nimbus.Contracts.DTOs.Features.Balloons;
namespace Nimbus.Application.Features.Balloons.Commands.UpdateBalloon;

public sealed record UpdateBalloonCommand(Guid Id, BalloonRequestDto Balloon) : IRequest<BalloonDto>;
