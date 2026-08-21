using MediatR;
using Nimbus.Application.Common.Exceptions;
using Nimbus.Application.Common.Interfaces;
using Nimbus.Application.Helpers;
using Nimbus.Domain.Entities;
using Nimbus.Domain.Interfaces;
namespace Nimbus.Application.Features.Auth.Command.ResendVerificationEmail;

public sealed class ResendVerificationEmailCommandHandler : IRequestHandler<ResendVerificationEmailCommand>
{
    private readonly IUserRepository _userRepository;
    private readonly IEmailQueue _emailQueue;
    private readonly EmailHelpers _emailHelpers;

    public ResendVerificationEmailCommandHandler(IUserRepository userRepository, IIdentityService identityService, IEmailQueue emailQueue, EmailHelpers emailHelpers)
    {
        _userRepository = userRepository;
        _emailQueue = emailQueue;
        _emailHelpers = emailHelpers;
    }

    public async Task Handle(ResendVerificationEmailCommand request, CancellationToken ct)
    { 
        var user = await _userRepository.GetByEmailAsync(request.Command.Email);
        
        if (user is null)
        {
            throw new NotFoundException(nameof(User), request.Command.Email);
        }
        
        if (user.EmailConfirmed)
        {
            throw new ProcessingException("Email already confirmed");
        }

        var email = await _emailHelpers.CreateEmailVerificationAsync(user.Id.ToString(), request.BaseUrl, user.FirstName, user.Email);
        await _emailQueue.EnqueueAsync(email, ct);
    }
}
