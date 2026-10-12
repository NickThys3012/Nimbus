using FluentValidation;
namespace Nimbus.Application.Features.Pilots.Queries.GetPilot;

public sealed class GetPilotQueryValidator : AbstractValidator<GetPilotQuery>
{
    public GetPilotQueryValidator()
    {
        RuleFor(q => q.Id).NotEmpty();
    }
}
