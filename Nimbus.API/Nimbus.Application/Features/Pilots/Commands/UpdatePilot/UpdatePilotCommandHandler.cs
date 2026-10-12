using MediatR;
using Nimbus.Application.Common.Exceptions;
using Nimbus.Application.Common.Interfaces;
using Nimbus.Application.Common.Ownership;
using Nimbus.Contracts.DTOs.Features.Pilots;
using Nimbus.Contracts.Mappers;
using Nimbus.Domain.Entities;
namespace Nimbus.Application.Features.Pilots.Commands.UpdatePilot;

public sealed class UpdatePilotCommandHandler : IRequestHandler<UpdatePilotCommand, PilotDto>
{
    private readonly ICurrentUserService _currentUser;
    private readonly IPilotService _pilots;

    public UpdatePilotCommandHandler(IPilotService pilots, ICurrentUserService currentUser)
    {
        _pilots = pilots;
        _currentUser = currentUser;
    }

    public async Task<PilotDto> Handle(UpdatePilotCommand request, CancellationToken ct)
    {
        var caller = new Caller(_currentUser.UserId, false);
        // A missing and a foreign pilot are indistinguishable: both are 404.
        if (await _pilots.UpdateAsync(caller, request.Id, request.Pilot.MapToEntity(), ct) == OwnedResult.NotFound)
        {
            throw new NotFoundException(nameof(Pilot), request.Id);
        }

        var updated = await _pilots.GetAsync(caller, request.Id, ct) ?? throw new NotFoundException(nameof(Pilot), request.Id);
        return updated.MapToDto();
    }
}
