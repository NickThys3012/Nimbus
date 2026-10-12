using MediatR;
using Nimbus.Contracts.DTOs.Features.Pilots;
namespace Nimbus.Application.Features.Pilots.Commands.UpdatePilot;

public sealed record UpdatePilotCommand(Guid Id, PilotRequestDto Pilot) : IRequest<PilotDto>;
