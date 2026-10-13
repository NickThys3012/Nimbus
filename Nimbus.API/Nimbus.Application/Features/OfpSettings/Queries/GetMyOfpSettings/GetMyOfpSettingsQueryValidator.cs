using FluentValidation;
namespace Nimbus.Application.Features.OfpSettings.Queries.GetMyOfpSettings;

/// <summary>The query has no parameters; the validator keeps the slice layout (and pipeline) uniform.</summary>
public sealed class GetMyOfpSettingsQueryValidator : AbstractValidator<GetMyOfpSettingsQuery> {}
