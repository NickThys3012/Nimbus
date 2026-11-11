using MediatR;
using Nimbus.Contracts.DTOs.Features.FlightPreparations;
using Nimbus.Domain.Enums;
namespace Nimbus.Application.Features.FlightPreparations.Images.Upload;

/// <param name="Content">The uploaded file. The caller owns disposal.</param>
/// <param name="DeclaredContentType">What the client claims it is; only a first filter, the bytes decide.</param>
public sealed record UploadFlightImageCommand(Guid FlightId, FlightPreparationSection Section, Stream Content, string DeclaredContentType)
    : IRequest<FlightImageDto>;
