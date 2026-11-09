using MediatR;
using Nimbus.Contracts.DTOs.Features.FlightPreparations;
namespace Nimbus.Application.Features.FlightPreparations.Commands.UpdateFlightPreparation;

public sealed record UpdateFlightPreparationCommand(Guid Id, FlightPreparationRequestDto Flight) : IRequest<FlightPreparationDto>;
