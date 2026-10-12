using FluentValidation;
namespace Nimbus.Application.Features.Balloons.Commands.CreateBalloon;

public sealed class CreateBalloonCommandValidator : AbstractValidator<CreateBalloonCommand>
{
    public CreateBalloonCommandValidator()
    {
        RuleFor(c => c.Balloon).NotNull().SetValidator(new BalloonRequestValidator());
    }
}
