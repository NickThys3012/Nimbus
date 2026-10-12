using FluentValidation;
namespace Nimbus.Application.Features.Balloons.Queries.GetBalloonUsage;

public sealed class GetBalloonUsageQueryValidator : AbstractValidator<GetBalloonUsageQuery>
{
    public GetBalloonUsageQueryValidator()
    {
        RuleFor(q => q.Id).NotEmpty();
    }
}
