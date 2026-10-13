using FluentValidation;
namespace Nimbus.Application.Features.LaunchLocations.Queries.GetLaunchLocation;

public sealed class GetLaunchLocationQueryValidator : AbstractValidator<GetLaunchLocationQuery>
{
    public GetLaunchLocationQueryValidator()
    {
        RuleFor(q => q.Id).NotEmpty();
    }
}
