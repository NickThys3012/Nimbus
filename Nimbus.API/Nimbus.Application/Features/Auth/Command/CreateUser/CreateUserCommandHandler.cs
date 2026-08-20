
using FluentValidation.Results;
using MediatR;
using Nimbus.Application.Abstraction;
using Nimbus.Application.Common.Exceptions;
using Nimbus.Application.Common.Interfaces;
using Nimbus.Domain.Interfaces;
namespace Nimbus.Application.Features.Auth.Command.CreateUser;

public sealed class CreateUserCommandHandler : IRequestHandler<CreateUserCommand>
{
    private readonly IUserRepository _userRepository;
    private readonly IIdentityService _identityService;
    private readonly IBusinessMetrics _businessMetrics;
    private readonly IEmailQueue _emailQueue;
    
    public CreateUserCommandHandler(IUserRepository userRepository, IIdentityService identityService, IBusinessMetrics businessMetrics, IEmailQueue emailQueue)
    {
        _userRepository = userRepository;
        _identityService = identityService;
        _businessMetrics = businessMetrics;
        _emailQueue = emailQueue;
    }
    public async Task Handle(CreateUserCommand request, CancellationToken ct)
    {
        var excisting = await _userRepository.GetByEmailAsync(request.Request.Email);
        if (excisting is not null)
        {
            throw new ValidationException([
                new ValidationFailure("Email", "Email already exists")
            ]);
        }    
        
        var id= await _identityService.RegisterAsync(request.Request.Email, request.Request.Password, request.Request.FirstName, request.Request.LastName);
        _businessMetrics.UsersRegistered();
        await _emailQueue.EnqueueAsync(new EmailMessage
        {
            ToAddress = request.Request.Email,
            Subject = "Welcome to Nimbus",
            TextBody = $"Hello {request.Request.FirstName},\n\nThank you for registering with Nimbus. We are excited to have you on board!\n\nBest regards,\nThe Nimbus Team",
            HtmlBody = $"<p>Hello {request.Request.FirstName},</p><p>Thank you for registering with Nimbus. We are excited to have you on board!</p><p>Best regards,<br/>The Nimbus Team</p>"
        }, ct);
    }
}
