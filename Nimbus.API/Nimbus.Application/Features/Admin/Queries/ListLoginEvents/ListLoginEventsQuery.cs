using MediatR;
using Nimbus.Contracts.DTOs.Features.Admin;
namespace Nimbus.Application.Features.Admin.Queries.ListLoginEvents;

public sealed record ListLoginEventsQuery(int Limit = ListLoginEventsQuery.DefaultLimit) : IRequest<IReadOnlyList<LoginEventDto>>
{
    public const int DefaultLimit = 50;
    public const int MaxLimit = 200;
}
