using FluentValidation;
namespace Nimbus.Application.Features.Balloons.Commands.UpdateBalloon;

public sealed class UpdateBalloonCommandValidator : AbstractValidator<UpdateBalloonCommand>
{
    public UpdateBalloonCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.Balloon).NotNull().SetValidator(new BalloonRequestValidator());
    }
}
