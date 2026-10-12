using MediatR;
using Nimbus.Application.Common.Exceptions;
using Nimbus.Application.Common.Interfaces;
using Nimbus.Application.Common.Ownership;
using Nimbus.Contracts.DTOs.Features.Pilots;
using Nimbus.Contracts.Mappers;
using Nimbus.Domain.Entities;
namespace Nimbus.Application.Features.Pilots.Queries.GetPilot;

public sealed class GetPilotQueryHandler : IRequestHandler<GetPilotQuery, PilotDto>
{
    private readonly ICurrentUserService _currentUser;
    private readonly IPilotService _pilots;

    public GetPilotQueryHandler(IPilotService pilots, ICurrentUserService currentUser)
    {
        _pilots = pilots;
        _currentUser = currentUser;
    }

    public async Task<PilotDto> Handle(GetPilotQuery request, CancellationToken ct)
    {
        var pilot = await _pilots.GetAsync(new Caller(_currentUser.UserId, false), request.Id, ct)
                    ?? throw new NotFoundException(nameof(Pilot), request.Id);
        return pilot.MapToDto();
    }
}
