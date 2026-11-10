using MediatR;
using Nimbus.Application.Common.Exceptions;
using Nimbus.Application.Common.Interfaces;
using Nimbus.Contracts.DTOs.Features.FlightPreparations;
using Nimbus.Contracts.Mappers;
using Nimbus.Domain.Entities;
using Nimbus.Domain.Interfaces;
namespace Nimbus.Application.Features.FlightPreparations.Queries.GetFlightPreparation;

/// <summary>The editable dossier, for the form. Only the owner edits: anyone else who may merely view it gets 403 (the UI then shows the read-only view).</summary>
public sealed class GetFlightPreparationQueryHandler : IRequestHandler<GetFlightPreparationQuery, FlightPreparationDto>
{
    private readonly ICurrentUserService _currentUser;
    private readonly IFlightDetailReader _access;
    private readonly IFlightPreparationRepository _flights;

    public GetFlightPreparationQueryHandler(IFlightPreparationRepository flights, IFlightDetailReader access, ICurrentUserService currentUser)
    {
        _flights = flights;
        _access = access;
        _currentUser = currentUser;
    }

    public async Task<FlightPreparationDto> Handle(GetFlightPreparationQuery request, CancellationToken ct)
    {
        var access = await _access.GetAccessAsync(_currentUser.UserId, _currentUser.IsAdmin, request.Id, ct);
        if (!access.Exists)
            throw new NotFoundException(nameof(FlightPreparation), request.Id);
        if (access.Role != FlightViewerRole.Owner)
            throw new ForbiddenException(nameof(FlightPreparation), request.Id);

        var flight = await _flights.GetByIdAsync(_currentUser.UserId, request.Id, ct)
                     ?? throw new NotFoundException(nameof(FlightPreparation), request.Id);
        return flight.MapToDto();
    }
}
