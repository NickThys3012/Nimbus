using FluentValidation.Results;
using MediatR;
using Nimbus.Application.Common.Exceptions;
using Nimbus.Application.Common.Interfaces;
using Nimbus.Application.Helpers;
using Nimbus.Domain.Interfaces;
namespace Nimbus.Application.Features.Auth.Command.CreateUser;

public sealed class CreateUserCommandHandler : IRequestHandler<CreateUserCommand, string>
{
    private readonly IUserRepository _userRepository;
    private readonly IIdentityService _identityService;
    private readonly IEmailQueue _emailQueue;
    private readonly IBusinessMetrics _businessMetrics;
    private readonly EmailHelpers _emailHelpers;
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
            throw new ValidationException([
                new ValidationFailure("Email", "Email already exists")
            ]);
        }

        var id = await _identityService.RegisterAsync(
            request.Request.Email,
            request.Request.Password,
            request.Request.FirstName,
            request.Request.LastName);

        var email = await _emailHelpers.CreateEmailVerificationAsync(id, request.BaseUrl, request.Request.FirstName, request.Request.Email);
        await _emailQueue.EnqueueAsync(email, ct);
        _businessMetrics.UsersRegistered();
        return id;
    } 
}
