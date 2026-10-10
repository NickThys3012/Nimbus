using FluentValidation;
namespace Nimbus.Application.Features.Admin.Commands.RecordLoginAttempt;

public sealed class RecordLoginAttemptCommandValidator : AbstractValidator<RecordLoginAttemptCommand>
{
    public RecordLoginAttemptCommandValidator()
    {
        // A successful sign-in always identifies the matched account.
        RuleFor(c => c.UserId)
            .NotNull().WithMessage("A successful login attempt must identify the matched user.")
            .When(c => c.Succeeded);
        RuleFor(c => c.UserId)
            .NotEqual(Guid.Empty)
            .When(c => c.UserId is not null);
        // The email is only ever stored for a matched account.
        RuleFor(c => c.Email)
            .NotEmpty()
            .MaximumLength(320)
            .When(c => c.UserId is not null);
        RuleFor(c => c.RemoteIp).MaximumLength(64);
    }
}
