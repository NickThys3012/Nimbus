using FluentValidation;
namespace Nimbus.Application.Features.Balloons.Queries.ListBalloons;

/// <summary>The query has no parameters; the validator exists to keep the slice layout (and pipeline) uniform.</summary>
public sealed class ListBalloonsQueryValidator : AbstractValidator<ListBalloonsQuery> {}
