using MediatR;
using Nimbus.Contracts.DTOs.Features.FlightPreparations;
namespace Nimbus.Application.Features.FlightPreparations.Images.List;

/// <summary>The flight's images with short-lived links, for the editor. Anyone who may see the flight may list them.</summary>
public sealed record ListFlightImagesQuery(Guid FlightId) : IRequest<IReadOnlyList<FlightImageDto>>;
