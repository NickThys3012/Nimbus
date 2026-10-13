using FluentValidation;
namespace Nimbus.Application.Features.LaunchLocations.Commands.DeleteLaunchLocation;

public sealed class DeleteLaunchLocationCommandValidator : AbstractValidator<DeleteLaunchLocationCommand>
{
    public DeleteLaunchLocationCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
    }
}
