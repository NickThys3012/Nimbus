using FluentValidation;
namespace Nimbus.Application.Features.Auth.Command.CreateUser;

public sealed class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserCommandValidator()
    {
        RuleFor(x => x.Request.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Request.Password).NotEmpty().MinimumLength(8).Matches(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&,.<>])[A-Za-z\d@$!%*?&,.<>]{8,}$")
            .WithMessage("Password must be at least 8 characters long and contain at least one uppercase letter, one lowercase letter, one number, and one special character.");
        RuleFor(x => x.Request.FirstName).NotEmpty();
        RuleFor(x => x.Request.LastName).NotEmpty();
    }
}
