using MediatR;
using Microsoft.Extensions.Logging;
using Nimbus.Application.Common.Exceptions;
using Nimbus.Application.Common.Interfaces;
using Nimbus.Domain.Entities;
using Nimbus.Domain.Interfaces;
namespace Nimbus.Application.Features.Admin.Commands.SetUserBlocked;

public sealed class SetUserBlockedCommandHandler : IRequestHandler<SetUserBlockedCommand>
{
    private readonly IUserAdministrationService _administration;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<SetUserBlockedCommandHandler> _logger;
    private readonly IUserRepository _userRepository;

    public SetUserBlockedCommandHandler(
        IUserRepository userRepository,
        IUserAdministrationService administration,
        ICurrentUserService currentUser,
        ILogger<SetUserBlockedCommandHandler> logger)
    {
        _userRepository = userRepository;
        _administration = administration;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task Handle(SetUserBlockedCommand request, CancellationToken ct)
    {
        var adminId = _currentUser.UserId;
        if (request.Blocked && adminId == request.UserId)
        {
            throw new ProcessingException("You cannot block your own account.");
        }

        var user = await _userRepository.GetByIdAsync(request.UserId)
                   ?? throw new NotFoundException(nameof(User), request.UserId);

        await _administration.SetBlockedAsync(user.Id, request.Blocked, ct);
        _logger.LogInformation("Admin {AdminUserId} {Action} user {TargetUserId}", adminId, request.Blocked ? "blocked" : "unblocked", user.Id);
    }
}
