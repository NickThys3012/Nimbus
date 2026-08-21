using MediatR;
using Nimbus.Application.Common.Exceptions;
using Nimbus.Domain.Entities;
using Nimbus.Domain.Interfaces;
namespace Nimbus.Application.Features.Auth.Command.UnblockVerificationEmail;

public sealed class UnblockVerificationEmailCommandHandler : IRequestHandler<UnblockVerificationEmailCommand>
{
    private readonly IUserRepository _userRepository;

    public UnblockVerificationEmailCommandHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task Handle(UnblockVerificationEmailCommand request, CancellationToken ct)
    {
        var user = await _userRepository.GetByEmailAsync(request.Email);
        if (user is null)
        {
            throw new NotFoundException(nameof(User), request.Email);
        }

        user.ResetVerificationEmailState();
        await _userRepository.UpdateAsync(user);
    }
}
