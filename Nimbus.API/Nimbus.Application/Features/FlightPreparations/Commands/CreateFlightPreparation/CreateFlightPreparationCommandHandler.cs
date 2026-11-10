using MediatR;
using Nimbus.Application.Common.Interfaces;
using Nimbus.Contracts.DTOs.Features.FlightPreparations;
using Nimbus.Contracts.Mappers;
using Nimbus.Domain.Entities;
using Nimbus.Domain.Enums;
using Nimbus.Domain.Interfaces;
namespace Nimbus.Application.Features.FlightPreparations.Commands.CreateFlightPreparation;

public sealed class CreateFlightPreparationCommandHandler : IRequestHandler<CreateFlightPreparationCommand, FlightPreparationDto>
{
    private readonly ICurrentUserService _currentUser;
    private readonly IFlightPreparationRepository _flights;
    private readonly IRichTextSanitizer _sanitizer;

    public CreateFlightPreparationCommandHandler(IFlightPreparationRepository flights, ICurrentUserService currentUser, IRichTextSanitizer sanitizer)
    {
        _flights = flights;
        _sanitizer = sanitizer;
        _currentUser = currentUser;
    }

    public async Task<FlightPreparationDto> Handle(CreateFlightPreparationCommand request, CancellationToken ct)
    {
        // A flight is always the caller's own, even for an admin; a new one is always planned.
        var flight = request.Flight.ApplyTo(new FlightPreparation { OwnerId = _currentUser.UserId, Status = FlightPreparationStatus.Planned });
        flight.PaxBriefingText = _sanitizer.Sanitize(flight.PaxBriefingText);
        await _flights.AddAsync(flight, ct);
        return flight.MapToDto();
    }
}
