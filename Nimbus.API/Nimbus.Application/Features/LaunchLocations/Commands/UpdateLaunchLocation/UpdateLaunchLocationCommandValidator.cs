using FluentValidation;
namespace Nimbus.Application.Features.LaunchLocations.Commands.UpdateLaunchLocation;

public sealed class UpdateLaunchLocationCommandValidator : AbstractValidator<UpdateLaunchLocationCommand>
{
    public UpdateLaunchLocationCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.LaunchLocation).NotNull().SetValidator(new LaunchLocationRequestValidator());
    }
}
