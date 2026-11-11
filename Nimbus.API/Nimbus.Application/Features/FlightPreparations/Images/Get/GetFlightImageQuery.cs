using MediatR;
using Nimbus.Domain.Interfaces;
namespace Nimbus.Application.Features.FlightPreparations.Images.Get;

/// <summary>The bytes of an image (or its thumbnail). The caller disposes the returned download.</summary>
public sealed record GetFlightImageQuery(Guid FlightId, Guid ImageId, bool Thumbnail) : IRequest<ObjectDownload>;
