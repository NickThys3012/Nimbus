using FluentValidation;
namespace Nimbus.Application.Features.Admin.Commands.RejectUser;

public sealed class RejectUserCommandValidator : AbstractValidator<RejectUserCommand>
{
    public RejectUserCommandValidator()
    {
        RuleFor(c => c.UserId).NotEmpty();
    }
}
