using FluentValidation;
namespace Nimbus.Application.Features.Auth.Command.UnblockVerificationEmail;

public sealed class UnblockVerificationEmailCommandValidator : AbstractValidator<UnblockVerificationEmailCommand>
{
    public UnblockVerificationEmailCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
    }
}
