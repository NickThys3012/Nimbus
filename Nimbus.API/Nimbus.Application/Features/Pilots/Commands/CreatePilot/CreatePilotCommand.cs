using MediatR;
using Nimbus.Contracts.DTOs.Features.Pilots;
namespace Nimbus.Application.Features.Pilots.Commands.CreatePilot;

public sealed record CreatePilotCommand(PilotRequestDto Pilot) : IRequest<PilotDto>;
