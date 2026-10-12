using MediatR;
using Nimbus.Contracts.DTOs.Features.Pilots;
namespace Nimbus.Application.Features.Pilots.Queries.GetPilot;

public sealed record GetPilotQuery(Guid Id) : IRequest<PilotDto>;
