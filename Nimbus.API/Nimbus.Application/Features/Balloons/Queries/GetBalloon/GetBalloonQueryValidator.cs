using FluentValidation;
namespace Nimbus.Application.Features.Balloons.Queries.GetBalloon;

public sealed class GetBalloonQueryValidator : AbstractValidator<GetBalloonQuery>
{
    public GetBalloonQueryValidator()
    {
        RuleFor(q => q.Id).NotEmpty();
    }
}
