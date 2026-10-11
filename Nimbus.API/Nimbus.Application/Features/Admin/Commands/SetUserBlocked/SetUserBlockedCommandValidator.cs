using FluentValidation;
namespace Nimbus.Application.Features.Admin.Commands.SetUserBlocked;

public sealed class SetUserBlockedCommandValidator : AbstractValidator<SetUserBlockedCommand>
{
    public SetUserBlockedCommandValidator()
    {
        RuleFor(c => c.UserId).NotEmpty();
    }
}
