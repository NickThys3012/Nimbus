using FluentValidation;
namespace Nimbus.Application.Features.GoNoGoThresholds.Queries.GetMyGoNoGoThresholds;

/// <summary>The query has no parameters; the validator keeps the slice layout (and pipeline) uniform.</summary>
public sealed class GetMyGoNoGoThresholdsQueryValidator : AbstractValidator<GetMyGoNoGoThresholdsQuery> {}
