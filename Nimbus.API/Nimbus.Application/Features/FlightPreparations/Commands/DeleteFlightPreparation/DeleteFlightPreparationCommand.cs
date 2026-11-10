using MediatR;
namespace Nimbus.Application.Features.FlightPreparations.Commands.DeleteFlightPreparation;

public sealed record DeleteFlightPreparationCommand(Guid Id) : IRequest;
