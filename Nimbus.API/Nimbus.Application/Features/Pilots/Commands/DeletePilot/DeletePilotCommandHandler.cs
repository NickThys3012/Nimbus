using MediatR;
using Nimbus.Application.Common.Exceptions;
using Nimbus.Application.Common.Interfaces;
using Nimbus.Application.Common.Ownership;
using Nimbus.Domain.Entities;
namespace Nimbus.Application.Features.Pilots.Commands.DeletePilot;

public sealed class DeletePilotCommandHandler : IRequestHandler<DeletePilotCommand>
{
    private readonly ICurrentUserService _currentUser;
    private readonly IPilotService _pilots;

    public DeletePilotCommandHandler(IPilotService pilots, ICurrentUserService currentUser)
    {
        _pilots = pilots;
        _currentUser = currentUser;
    }

    public async Task Handle(DeletePilotCommand request, CancellationToken ct)
    {
        if (await _pilots.DeleteAsync(new Caller(_currentUser.UserId, false), request.Id, ct) == OwnedResult.NotFound)
        {
            throw new NotFoundException(nameof(Pilot), request.Id);
        }
    }
}
