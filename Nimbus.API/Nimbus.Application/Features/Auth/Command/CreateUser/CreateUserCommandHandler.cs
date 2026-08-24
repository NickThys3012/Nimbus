using MediatR;
using Nimbus.Application.Common.Exceptions;
using Nimbus.Application.Common.Interfaces;
using Nimbus.Application.Helpers;
using Nimbus.Domain.Entities;
using Nimbus.Domain.Interfaces;
namespace Nimbus.Application.Features.Auth.Command.CreateUser;

public sealed class CreateUserCommandHandler : IRequestHandler<CreateUserCommand, string>
{
    private const int MaxVerificationEmailSends = 5;
    private static readonly TimeSpan VerificationEmailCooldown = TimeSpan.FromMinutes(15);
    private readonly IBusinessMetrics _businessMetrics;
    private readonly EmailHelpers _emailHelpers;
    private readonly IEmailQueue _emailQueue;
    private readonly IIdentityService _identityService;
    private readonly IUserRepository _userRepository;
    public CreateUserCommandHandler(
        IUserRepository userRepository,
        IIdentityService identityService,
        IEmailQueue emailQueue,
        IBusinessMetrics businessMetrics,
        EmailHelpers emailHelpers)
    {
        _userRepository = userRepository;
        _identityService = identityService;
        _emailQueue = emailQueue;
        _businessMetrics = businessMetrics;
        _emailHelpers = emailHelpers;
    }

    public async Task<string> Handle(CreateUserCommand request, CancellationToken ct)
    {
        var existing = await _userRepository.GetByEmailAsync(request.Request.Email);
        if (existing is not null)
        {
            throw new DuplicateException("Email", "Email already exists");
        }

        var id = await _identityService.RegisterAsync(
            request.Request.Email,
            request.Request.Password,
            request.Request.FirstName,
            request.Request.LastName);

        var user = await _userRepository.GetByIdAsync(Guid.Parse(id));
        if (user is null)
        {
            throw new ProcessingException(nameof(User), id);
        }

        var email = await _emailHelpers.CreateEmailVerificationAsync(id, request.BaseUrl, request.Request.FirstName, request.Request.Email);
        user.RecordVerificationEmailSent(DateTimeOffset.UtcNow.Add(VerificationEmailCooldown), MaxVerificationEmailSends);

        await _userRepository.UpdateAsync(user);
        await _emailQueue.EnqueueAsync(email, ct);

        _businessMetrics.UsersRegistered();
        return id;
    }
}
