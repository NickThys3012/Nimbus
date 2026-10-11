using FluentValidation;
namespace Nimbus.Application.Features.Admin.Queries.ListLoginEvents;

public sealed class ListLoginEventsQueryValidator : AbstractValidator<ListLoginEventsQuery>
{
    public ListLoginEventsQueryValidator()
    {
        RuleFor(q => q.Limit).InclusiveBetween(1, ListLoginEventsQuery.MaxLimit);
    }
}
