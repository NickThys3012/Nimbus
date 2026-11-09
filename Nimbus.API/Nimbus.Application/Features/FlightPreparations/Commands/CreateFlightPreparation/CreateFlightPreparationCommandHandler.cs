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

    public CreateFlightPreparationCommandHandler(IFlightPreparationRepository flights, ICurrentUserService currentUser)
    {
        _flights = flights;
        _currentUser = currentUser;
    }

    public async Task<FlightPreparationDto> Handle(CreateFlightPreparationCommand request, CancellationToken ct)
    {
        // A flight is always the caller's own, even for an admin; a new one is always planned.
        var flight = request.Flight.ApplyTo(new FlightPreparation { OwnerId = _currentUser.UserId, Status = FlightPreparationStatus.Planned });
        await _flights.AddAsync(flight, ct);
        return flight.MapToDto();
    }
}
