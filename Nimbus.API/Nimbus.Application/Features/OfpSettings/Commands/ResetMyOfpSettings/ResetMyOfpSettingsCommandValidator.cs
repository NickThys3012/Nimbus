using FluentValidation;
namespace Nimbus.Application.Features.OfpSettings.Commands.ResetMyOfpSettings;

/// <summary>The command has no parameters; the validator keeps the slice layout (and pipeline) uniform.</summary>
public sealed class ResetMyOfpSettingsCommandValidator : AbstractValidator<ResetMyOfpSettingsCommand> {}
