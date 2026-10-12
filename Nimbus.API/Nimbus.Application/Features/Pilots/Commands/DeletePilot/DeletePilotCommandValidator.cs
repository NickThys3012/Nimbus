using FluentValidation;
namespace Nimbus.Application.Features.Pilots.Commands.DeletePilot;

public sealed class DeletePilotCommandValidator : AbstractValidator<DeletePilotCommand>
{
    public DeletePilotCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
    }
}
