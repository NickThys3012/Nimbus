using MediatR;
using Microsoft.Extensions.Logging;
using Nimbus.Application.Common.Exceptions;
using Nimbus.Application.Common.Interfaces;
using Nimbus.Domain.Entities;
using Nimbus.Domain.Enums;
using Nimbus.Domain.Interfaces;
namespace Nimbus.Application.Features.Admin.Commands.DeleteUser;

public sealed class DeleteUserCommandHandler : IRequestHandler<DeleteUserCommand>
{
    private readonly IUserAdministrationService _administration;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<DeleteUserCommandHandler> _logger;
    private readonly IUserRepository _userRepository;

    public DeleteUserCommandHandler(
        IUserRepository userRepository,
        IUserAdministrationService administration,
        ICurrentUserService currentUser,
        ILogger<DeleteUserCommandHandler> logger)
    {
        _userRepository = userRepository;
        _administration = administration;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task Handle(DeleteUserCommand request, CancellationToken ct)
    {
        var adminId = _currentUser.UserId;
        if (adminId == request.UserId)
        {
            throw new ProcessingException("You cannot delete your own account.");
        }

        var user = await _userRepository.GetByIdAsync(request.UserId)
                   ?? throw new NotFoundException(nameof(User), request.UserId);

        // The "last active Admin" rule is enforced atomically inside IUserAdministrationService.
        await _administration.DeleteAsync(user.Id, ct);
        _logger.LogInformation("Admin {AdminUserId} deleted user {TargetUserId}", adminId, user.Id);
    }
}
