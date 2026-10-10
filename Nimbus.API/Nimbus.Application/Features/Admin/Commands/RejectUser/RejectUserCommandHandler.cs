using MediatR;
using Microsoft.Extensions.Logging;
using Nimbus.Application.Common.Exceptions;
using Nimbus.Application.Common.Interfaces;
using Nimbus.Domain.Entities;
using Nimbus.Domain.Interfaces;
namespace Nimbus.Application.Features.Admin.Commands.RejectUser;

public sealed class RejectUserCommandHandler : IRequestHandler<RejectUserCommand>
{
    private readonly IUserAdministrationService _administration;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<RejectUserCommandHandler> _logger;
    private readonly IUserRepository _userRepository;

    public RejectUserCommandHandler(
        IUserRepository userRepository,
        IUserAdministrationService administration,
        ICurrentUserService currentUser,
        ILogger<RejectUserCommandHandler> logger)
    {
        _userRepository = userRepository;
        _administration = administration;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task Handle(RejectUserCommand request, CancellationToken ct)
    {
        var adminId = _currentUser.UserId;
        if (adminId == request.UserId)
        {
            throw new ProcessingException("You cannot reject your own account.");
        }

        var user = await _userRepository.GetByIdAsync(request.UserId)
                   ?? throw new NotFoundException(nameof(User), request.UserId);

        user.Reject(); // throws DomainException (HTTP 400) for an already approved user
        await _userRepository.UpdateAsync(user);
        await _administration.RevokeSessionsAsync(user.Id, ct);

        _logger.LogInformation("Admin {AdminUserId} rejected user {TargetUserId}", adminId, user.Id);
    }
}
