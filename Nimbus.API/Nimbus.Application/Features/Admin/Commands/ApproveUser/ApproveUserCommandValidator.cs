using FluentValidation;
namespace Nimbus.Application.Features.Admin.Commands.ApproveUser;

public sealed class ApproveUserCommandValidator : AbstractValidator<ApproveUserCommand>
{
    public ApproveUserCommandValidator()
    {
        RuleFor(c => c.UserId).NotEmpty();
    }
}
