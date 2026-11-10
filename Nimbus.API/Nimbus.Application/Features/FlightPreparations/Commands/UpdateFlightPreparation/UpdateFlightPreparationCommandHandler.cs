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
    private readonly IFlightDetailReader _access;
    private readonly IFlightPreparationRepository _flights;
    private readonly IRichTextSanitizer _sanitizer;

    public UpdateFlightPreparationCommandHandler(
        IFlightPreparationRepository flights, IFlightDetailReader access, ICurrentUserService currentUser, IRichTextSanitizer sanitizer)
    {
        _flights = flights;
        _access = access;
        _sanitizer = sanitizer;
        _currentUser = currentUser;
    }

    public async Task<FlightPreparationDto> Handle(UpdateFlightPreparationCommand request, CancellationToken ct)
    {
        // The owner and an admin may change a flight; anyone else who can merely see it is told so (403).
        var access = await _access.GetAccessAsync(_currentUser.UserId, _currentUser.IsAdmin, request.Id, ct);
        if (!access.Exists)
            throw new NotFoundException(nameof(FlightPreparation), request.Id);
        if (access.Role is not (FlightViewerRole.Owner or FlightViewerRole.Admin))
            throw new ForbiddenException(nameof(FlightPreparation), request.Id);

        // The repository is owner-scoped; an admin edits on the flight owner's behalf, so it is asked as the owner.
        var owner = access.OwnerId!.Value;

        // Start from the stored row: the repository writes every column of what it is given, so the
        // columns the form does not own (outcome, simulation settings, status) must travel along unchanged.
        var flight = await _flights.GetByIdAsync(owner, request.Id, ct)
                     ?? throw new NotFoundException(nameof(FlightPreparation), request.Id);
        request.Flight.ApplyTo(flight);
        flight.PaxBriefingText = _sanitizer.Sanitize(flight.PaxBriefingText);

        if (!await _flights.UpdateAsync(owner, flight, ct))
            throw new NotFoundException(nameof(FlightPreparation), request.Id);
        return flight.MapToDto();
    }
}
