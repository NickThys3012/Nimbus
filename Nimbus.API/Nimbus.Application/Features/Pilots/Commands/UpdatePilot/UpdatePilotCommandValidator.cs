using FluentValidation;
namespace Nimbus.Application.Features.Pilots.Commands.UpdatePilot;

public sealed class UpdatePilotCommandValidator : AbstractValidator<UpdatePilotCommand>
{
    public UpdatePilotCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.Pilot).NotNull().SetValidator(new PilotRequestValidator());
    }
}
