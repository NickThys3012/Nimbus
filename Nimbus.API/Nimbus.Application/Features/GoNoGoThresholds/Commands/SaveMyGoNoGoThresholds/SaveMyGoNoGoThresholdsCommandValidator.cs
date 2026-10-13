using FluentValidation;
namespace Nimbus.Application.Features.GoNoGoThresholds.Commands.SaveMyGoNoGoThresholds;

public sealed class SaveMyGoNoGoThresholdsCommandValidator : AbstractValidator<SaveMyGoNoGoThresholdsCommand>
{
    public SaveMyGoNoGoThresholdsCommandValidator()
    {
        RuleFor(c => c.Thresholds).NotNull().SetValidator(new GoNoGoThresholdsRequestValidator());
    }
}
