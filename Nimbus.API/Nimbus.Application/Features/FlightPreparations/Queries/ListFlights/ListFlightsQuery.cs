using MediatR;
using Nimbus.Contracts.DTOs.Features.FlightPreparations;
using Nimbus.Domain.Enums;
namespace Nimbus.Application.Features.FlightPreparations.Queries.ListFlights;

/// <summary>The signed-in user's flights (own + shared with them; admin: all), filtered, sorted and paged in SQL.</summary>
public sealed record ListFlightsQuery(FlightListFilter Filter, FlightListSort Sort, bool Descending, int Page, int PageSize)
    : IRequest<FlightListPageDto>
{
    public const int MaxPageSize = 100;
}
