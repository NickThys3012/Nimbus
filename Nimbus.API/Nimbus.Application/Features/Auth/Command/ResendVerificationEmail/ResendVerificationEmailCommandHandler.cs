using MediatR;
using Nimbus.Application.Common.Exceptions;
using Nimbus.Application.Common.Interfaces;
using Nimbus.Application.Helpers;
using Nimbus.Domain.Entities;
using Nimbus.Domain.Interfaces;
namespace Nimbus.Application.Features.Auth.Command.ResendVerificationEmail;

public sealed class ResendVerificationEmailCommandHandler : IRequestHandler<ResendVerificationEmailCommand>
{
    private const int MaxVerificationEmailSends = 5;
    private static readonly TimeSpan VerificationEmailCooldown = TimeSpan.FromMinutes(15);
    private readonly TimeProvider _clock;
    private readonly EmailHelpers _emailHelpers;
    private readonly IEmailQueue _emailQueue;
    private readonly IUserRepository _userRepository;

    public ResendVerificationEmailCommandHandler(IUserRepository userRepository, IEmailQueue emailQueue, EmailHelpers emailHelpers, TimeProvider clock)
    {
        _userRepository = userRepository;
        _emailQueue = emailQueue;
        _emailHelpers = emailHelpers;
        _clock = clock;
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

        var now = _clock.GetUtcNow();
        if (user.VerificationEmailLocked)
        {
            throw new ProcessingException("Verification emails are locked. Ask an admin to unblock the account.");
        }

        if (user.VerificationEmailCooldownEndUtc is not null && user.VerificationEmailCooldownEndUtc > now)
        {
            throw new ProcessingException("Please wait before requesting another verification email.");
        }

        var email = await _emailHelpers.CreateEmailVerificationAsync(user.Id.ToString(), request.BaseUrl, user.FirstName, user.Email);
        user.RecordVerificationEmailSent(now.Add(VerificationEmailCooldown), MaxVerificationEmailSends);
        await _userRepository.UpdateAsync(user);
        await _emailQueue.EnqueueAsync(email, ct);
    }
}
