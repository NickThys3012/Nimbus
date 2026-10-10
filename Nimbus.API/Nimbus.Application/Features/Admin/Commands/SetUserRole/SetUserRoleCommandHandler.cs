using MediatR;
using Microsoft.Extensions.Logging;
using Nimbus.Application.Common.Exceptions;
using Nimbus.Application.Common.Interfaces;
using Nimbus.Domain.Entities;
using Nimbus.Domain.Enums;
using Nimbus.Domain.Interfaces;
namespace Nimbus.Application.Features.Admin.Commands.SetUserRole;

public sealed class SetUserRoleCommandHandler : IRequestHandler<SetUserRoleCommand>
{
    private readonly IUserAdministrationService _administration;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<SetUserRoleCommandHandler> _logger;
    private readonly IUserRepository _userRepository;

    public SetUserRoleCommandHandler(
        IUserRepository userRepository,
        IUserAdministrationService administration,
        ICurrentUserService currentUser,
        ILogger<SetUserRoleCommandHandler> logger)
    {
        _userRepository = userRepository;
        _administration = administration;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task Handle(SetUserRoleCommand request, CancellationToken ct)
    {
        var adminId = _currentUser.UserId;
        var user = await _userRepository.GetByIdAsync(request.UserId)
                   ?? throw new NotFoundException(nameof(User), request.UserId);

        var adminRole = nameof(UserRole.Admin);
        var demotesAdmin = user.Roles.Contains(adminRole) && request.Role != adminRole;
        if (demotesAdmin)
        {
            if (adminId == user.Id)
            {
                throw new ProcessingException("You cannot remove your own Admin role.");
            }
            if (await _administration.CountUsersInRoleAsync(adminRole, ct) <= 1)
            {
                throw new ProcessingException("The last Admin cannot be demoted.");
            }
        }

        await _administration.SetRoleAsync(user.Id, request.Role, ct);
        _logger.LogInformation("Admin {AdminUserId} set role of user {TargetUserId} to {Role}", adminId, user.Id, request.Role);
    }
}
