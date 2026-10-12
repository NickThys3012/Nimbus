using FluentValidation;
namespace Nimbus.Application.Features.Pilots.Commands.CreatePilot;

public sealed class CreatePilotCommandValidator : AbstractValidator<CreatePilotCommand>
{
    public CreatePilotCommandValidator()
    {
        RuleFor(c => c.Pilot).NotNull().SetValidator(new PilotRequestValidator());
    }
}
