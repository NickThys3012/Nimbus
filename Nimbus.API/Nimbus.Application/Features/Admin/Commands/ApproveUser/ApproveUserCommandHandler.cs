using MediatR;
using Microsoft.Extensions.Logging;
using Nimbus.Application.Common.Exceptions;
using Nimbus.Application.Common.Interfaces;
using Nimbus.Domain.Entities;
using Nimbus.Domain.Interfaces;
namespace Nimbus.Application.Features.Admin.Commands.ApproveUser;

public sealed class ApproveUserCommandHandler : IRequestHandler<ApproveUserCommand>
{
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<ApproveUserCommandHandler> _logger;
    private readonly IUserRepository _userRepository;

    public ApproveUserCommandHandler(
        IUserRepository userRepository,
        ICurrentUserService currentUser,
        ILogger<ApproveUserCommandHandler> logger)
    {
        _userRepository = userRepository;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task Handle(ApproveUserCommand request, CancellationToken ct)
    {
        // Admin identity comes from the authenticated principal, never from the request.
        var adminId = _currentUser.UserId;
        var user = await _userRepository.GetByIdAsync(request.UserId)
                   ?? throw new NotFoundException(nameof(User), request.UserId);

        user.Approve();
        await _userRepository.UpdateAsync(user);

        _logger.LogInformation("Admin {AdminUserId} approved user {TargetUserId}", adminId, user.Id);
    }
}
