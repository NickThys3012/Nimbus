using FluentValidation;
namespace Nimbus.Application.Features.GoNoGoThresholds.Commands.ResetMyGoNoGoThresholds;

/// <summary>The command has no parameters; the validator keeps the slice layout (and pipeline) uniform.</summary>
public sealed class ResetMyGoNoGoThresholdsCommandValidator : AbstractValidator<ResetMyGoNoGoThresholdsCommand> {}
