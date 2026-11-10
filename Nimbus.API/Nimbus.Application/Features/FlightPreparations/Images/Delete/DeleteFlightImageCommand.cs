using MediatR;
namespace Nimbus.Application.Features.FlightPreparations.Images.Delete;

public sealed record DeleteFlightImageCommand(Guid FlightId, Guid ImageId) : IRequest;
