using MediatR;
using Nimbus.Contracts.DTOs.Features.Pilots;
namespace Nimbus.Application.Features.Pilots.Queries.ListPilots;

public sealed record ListPilotsQuery : IRequest<IReadOnlyList<PilotDto>>;
