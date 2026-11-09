using MediatR;
using Nimbus.Contracts.DTOs.Features.FlightPreparations;
namespace Nimbus.Application.Features.FlightPreparations.Commands.CreateFlightPreparation;

public sealed record CreateFlightPreparationCommand(FlightPreparationRequestDto Flight) : IRequest<FlightPreparationDto>;
