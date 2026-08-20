
using FluentValidation.Results;
using MediatR;
using Nimbus.Application.Common.Exceptions;
using Nimbus.Application.Common.Interfaces;
using Nimbus.Domain.Interfaces;
namespace Nimbus.Application.Features.Auth.Command.CreateUser;

public sealed class CreateUserCommandHandler : IRequestHandler<CreateUserCommand, string>
{
    private readonly IUserRepository _userRepository;
    private readonly IIdentityService _identityService;
    private readonly IBusinessMetrics _businessMetrics;
    
    public CreateUserCommandHandler(
        IUserRepository userRepository,
        IIdentityService identityService,
        IBusinessMetrics businessMetrics)
    {
        _userRepository = userRepository;
        _identityService = identityService;
        _businessMetrics = businessMetrics;
    }

    public async Task<string> Handle(CreateUserCommand request, CancellationToken ct)
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
        return id;
    }
}
