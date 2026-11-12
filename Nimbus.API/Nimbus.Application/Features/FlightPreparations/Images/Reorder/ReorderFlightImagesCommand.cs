using MediatR;
using Nimbus.Domain.Enums;
namespace Nimbus.Application.Features.FlightPreparations.Images.Reorder;

public sealed record ReorderFlightImagesCommand(Guid FlightId, FlightPreparationSection Section, IReadOnlyList<Guid> ImageIds) : IRequest;
