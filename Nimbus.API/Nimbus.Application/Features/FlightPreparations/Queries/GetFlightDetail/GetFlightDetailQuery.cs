using MediatR;
using Nimbus.Contracts.DTOs.Features.FlightPreparations;
namespace Nimbus.Application.Features.FlightPreparations.Queries.GetFlightDetail;

public sealed record GetFlightDetailQuery(Guid Id) : IRequest<FlightDetailDto>;
