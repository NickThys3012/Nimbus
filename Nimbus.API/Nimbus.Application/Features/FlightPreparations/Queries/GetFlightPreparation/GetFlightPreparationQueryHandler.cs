using MediatR;
using Nimbus.Application.Common.Exceptions;
using Nimbus.Application.Common.Interfaces;
using Nimbus.Contracts.DTOs.Features.FlightPreparations;
using Nimbus.Contracts.Mappers;
using Nimbus.Domain.Entities;
using Nimbus.Domain.Interfaces;
namespace Nimbus.Application.Features.FlightPreparations.Queries.GetFlightPreparation;

public sealed class GetFlightPreparationQueryHandler : IRequestHandler<GetFlightPreparationQuery, FlightPreparationDto>
{
    private readonly ICurrentUserService _currentUser;
    private readonly IFlightPreparationRepository _flights;

    public GetFlightPreparationQueryHandler(IFlightPreparationRepository flights, ICurrentUserService currentUser)
    {
        _flights = flights;
        _currentUser = currentUser;
    }

    public async Task<FlightPreparationDto> Handle(GetFlightPreparationQuery request, CancellationToken ct)
    {
        // Owner only for now: another user's flight answers 404, same as a missing one.
        var flight = await _flights.GetByIdAsync(_currentUser.UserId, request.Id, ct)
                     ?? throw new NotFoundException(nameof(FlightPreparation), request.Id);
        return flight.MapToDto();
    }
}
