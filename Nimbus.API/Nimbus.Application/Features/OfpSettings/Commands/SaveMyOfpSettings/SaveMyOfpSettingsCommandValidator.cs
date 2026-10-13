using FluentValidation;
namespace Nimbus.Application.Features.OfpSettings.Commands.SaveMyOfpSettings;

public sealed class SaveMyOfpSettingsCommandValidator : AbstractValidator<SaveMyOfpSettingsCommand>
{
    public SaveMyOfpSettingsCommandValidator()
    {
        RuleFor(c => c.Settings).NotNull().SetValidator(new OfpSettingsRequestValidator());
    }
}
