using MediatR;
using Nimbus.Application.Common.Interfaces;
using Nimbus.Application.Common.Ownership;
using Nimbus.Contracts.DTOs.Features.Pilots;
using Nimbus.Contracts.Mappers;
namespace Nimbus.Application.Features.Pilots.Commands.CreatePilot;

public sealed class CreatePilotCommandHandler : IRequestHandler<CreatePilotCommand, PilotDto>
{
    private readonly ICurrentUserService _currentUser;
    private readonly IPilotService _pilots;

    public CreatePilotCommandHandler(IPilotService pilots, ICurrentUserService currentUser)
    {
        _pilots = pilots;
        _currentUser = currentUser;
    }

    public async Task<PilotDto> Handle(CreatePilotCommand request, CancellationToken ct)
    {
        // "My pilots" is always the caller's own, even for an admin.
        var created = await _pilots.CreateAsync(new Caller(_currentUser.UserId, false), request.Pilot.MapToEntity(), ct);
        return created.MapToDto();
    }
}
