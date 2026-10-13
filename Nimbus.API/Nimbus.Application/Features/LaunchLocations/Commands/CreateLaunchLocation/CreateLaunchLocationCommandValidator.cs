using FluentValidation;
namespace Nimbus.Application.Features.LaunchLocations.Commands.CreateLaunchLocation;

public sealed class CreateLaunchLocationCommandValidator : AbstractValidator<CreateLaunchLocationCommand>
{
    public CreateLaunchLocationCommandValidator()
    {
        RuleFor(c => c.LaunchLocation).NotNull().SetValidator(new LaunchLocationRequestValidator());
    }
}
