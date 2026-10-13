using FluentValidation;
namespace Nimbus.Application.Features.Profile.Commands.SaveMyProfile;

public sealed class SaveMyProfileCommandValidator : AbstractValidator<SaveMyProfileCommand>
{
    public SaveMyProfileCommandValidator()
    {
        RuleFor(c => c.Profile).NotNull().SetValidator(new ProfileRequestValidator());
    }
}
