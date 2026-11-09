using FluentValidation;
namespace Nimbus.Application.Features.FlightPreparations.Queries.ListFlights;

public sealed class ListFlightsQueryValidator : AbstractValidator<ListFlightsQuery>
{
    public ListFlightsQueryValidator()
    {
        RuleFor(q => q.Filter).IsInEnum();
        RuleFor(q => q.Sort).IsInEnum();
        RuleFor(q => q.Page).GreaterThanOrEqualTo(1);
        RuleFor(q => q.PageSize).InclusiveBetween(1, ListFlightsQuery.MaxPageSize);
    }
}
