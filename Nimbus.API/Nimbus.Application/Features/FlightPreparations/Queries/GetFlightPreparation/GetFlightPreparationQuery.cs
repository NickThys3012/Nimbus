using MediatR;
using Nimbus.Contracts.DTOs.Features.FlightPreparations;
namespace Nimbus.Application.Features.FlightPreparations.Queries.GetFlightPreparation;

public sealed record GetFlightPreparationQuery(Guid Id) : IRequest<FlightPreparationDto>;
