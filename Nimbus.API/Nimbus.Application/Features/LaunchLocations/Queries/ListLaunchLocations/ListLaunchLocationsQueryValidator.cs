using FluentValidation;
namespace Nimbus.Application.Features.LaunchLocations.Queries.ListLaunchLocations;

/// <summary>The query has no parameters; the validator exists to keep the slice layout (and pipeline) uniform.</summary>
public sealed class ListLaunchLocationsQueryValidator : AbstractValidator<ListLaunchLocationsQuery> {}
