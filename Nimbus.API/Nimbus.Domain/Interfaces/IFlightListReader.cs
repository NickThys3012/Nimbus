using Nimbus.Domain.Enums;
using Nimbus.Domain.Services;
namespace Nimbus.Domain.Interfaces;

/// <summary>What the list should return. <c>UserId</c>/<c>IsAdmin</c> come from the auth context, never from client input.</summary>
public sealed record FlightListCriteria(
    Guid UserId, bool IsAdmin, FlightListFilter Filter, FlightListSort Sort, bool Descending, int Page, int PageSize);

/// <summary>One list row: scalar columns only, plus the plain values the assessment service needs.</summary>
public sealed record FlightListRow(
    Guid Id,
    string Title,
    DateOnly FlightDate,
    TimeOnly FlightTime,
    string? BalloonRegistration,
    string? LocationName,
    FlightPreparationStatus Status,
    DateTime? FlownAt,
    Guid OwnerId,
    FlightAssessmentInput Assessment);

public sealed record FlightListPage(IReadOnlyList<FlightListRow> Items, int TotalCount);

/// <summary>
///     Read side of the flight list (issue #40). Filtering, ordering and paging run in SQL; only a
///     lightweight projection is loaded, never <c>FlightPreparation</c> entities or their children.
/// </summary>
public interface IFlightListReader
{
    Task<FlightListPage> GetPageAsync(FlightListCriteria criteria, CancellationToken ct = default);
}

/// <summary>Resolves display names for many users in one batched lookup.</summary>
public interface IUserDirectory
{
    Task<IReadOnlyDictionary<Guid, string>> GetDisplayNamesAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct = default);
}
