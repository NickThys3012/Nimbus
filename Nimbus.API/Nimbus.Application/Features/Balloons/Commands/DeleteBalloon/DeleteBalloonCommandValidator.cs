using FluentValidation;
namespace Nimbus.Application.Features.Balloons.Commands.DeleteBalloon;

public sealed class DeleteBalloonCommandValidator : AbstractValidator<DeleteBalloonCommand>
{
    public DeleteBalloonCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
    }
}
