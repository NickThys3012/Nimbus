using FluentValidation;
namespace Nimbus.Application.Features.Profile.Queries.GetMyProfile;

/// <summary>The query has no parameters; the validator keeps the slice layout (and pipeline) uniform.</summary>
public sealed class GetMyProfileQueryValidator : AbstractValidator<GetMyProfileQuery> {}
