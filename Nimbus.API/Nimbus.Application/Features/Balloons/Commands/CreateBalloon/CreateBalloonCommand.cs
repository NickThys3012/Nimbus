using MediatR;
using Nimbus.Contracts.DTOs.Features.Balloons;
namespace Nimbus.Application.Features.Balloons.Commands.CreateBalloon;

public sealed record CreateBalloonCommand(BalloonRequestDto Balloon) : IRequest<BalloonDto>;
