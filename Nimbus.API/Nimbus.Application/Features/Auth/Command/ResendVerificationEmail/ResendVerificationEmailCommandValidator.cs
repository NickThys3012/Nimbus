using FluentValidation;
namespace Nimbus.Application.Features.Auth.Command.ResendVerificationEmail;

public sealed class ResendVerificationEmailCommandValidator : AbstractValidator<ResendVerificationEmailCommand>
{
    public ResendVerificationEmailCommandValidator()
    {
        RuleFor(x => x.Command.Email).NotEmpty().EmailAddress();
    }
}
