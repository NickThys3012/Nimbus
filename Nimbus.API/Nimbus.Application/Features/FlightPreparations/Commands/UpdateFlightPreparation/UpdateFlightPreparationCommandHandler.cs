using MediatR;
using Nimbus.Application.Common.Exceptions;
using Nimbus.Application.Common.Interfaces;
using Nimbus.Contracts.DTOs.Features.FlightPreparations;
using Nimbus.Contracts.Mappers;
using Nimbus.Domain.Entities;
using Nimbus.Domain.Interfaces;
namespace Nimbus.Application.Features.FlightPreparations.Commands.UpdateFlightPreparation;

public sealed class UpdateFlightPreparationCommandHandler : IRequestHandler<UpdateFlightPreparationCommand, FlightPreparationDto>
{
    private readonly ICurrentUserService _currentUser;
    private readonly IFlightPreparationRepository _flights;
    private readonly IRichTextSanitizer _sanitizer;

    public UpdateFlightPreparationCommandHandler(IFlightPreparationRepository flights, ICurrentUserService currentUser, IRichTextSanitizer sanitizer)
    {
        _flights = flights;
        _sanitizer = sanitizer;
        _currentUser = currentUser;
    }

    public async Task<FlightPreparationDto> Handle(UpdateFlightPreparationCommand request, CancellationToken ct)
    {
        var me = _currentUser.UserId;

        // Start from the stored row: the repository writes every column of what it is given, so the
        // columns the form does not own (outcome, simulation settings, status) must travel along unchanged.
        var flight = await _flights.GetByIdAsync(me, request.Id, ct)
                     ?? throw new NotFoundException(nameof(FlightPreparation), request.Id);
        request.Flight.ApplyTo(flight);
        flight.PaxBriefingText = _sanitizer.Sanitize(flight.PaxBriefingText);

        if (!await _flights.UpdateAsync(me, flight, ct))
            throw new NotFoundException(nameof(FlightPreparation), request.Id);
        return flight.MapToDto();
    }
}
