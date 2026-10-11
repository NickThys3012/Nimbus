using MediatR;
using Nimbus.Domain.Interfaces;
namespace Nimbus.Application.Features.Auth.Command.ResetPassword;

public sealed class ResetPasswordCommandHandler : IRequestHandler<ResetPasswordCommand, bool>
{
    private readonly IUserAdministrationService _administration;
    private readonly IIdentityService _identityService;
    private readonly IUserRepository _userRepository;

    public ResetPasswordCommandHandler(IIdentityService identityService, IUserRepository userRepository, IUserAdministrationService administration)
    {
        _identityService = identityService;
        _userRepository = userRepository;
        _administration = administration;
    }

    public async Task<bool> Handle(ResetPasswordCommand request, CancellationToken ct)
    {
        if (!await _identityService.ResetPasswordAsync(request.Email, request.Token, request.NewPassword, ct))
        {
            return false;
        }

        // A reset implies the old credential may be compromised: end every existing session.
        var user = await _userRepository.GetByEmailAsync(request.Email);
        if (user is not null)
        {
            await _administration.RevokeSessionsAsync(user.Id, ct);
        }

        return true;
    }
}
