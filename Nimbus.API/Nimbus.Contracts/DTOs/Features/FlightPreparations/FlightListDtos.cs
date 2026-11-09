namespace Nimbus.Contracts.DTOs.Features.FlightPreparations;

/// <summary>
///     One row of the flight list. <c>GoNoGoVerdict</c> is "green", "yellow", "red" or "unknown" and comes from the single
///     assessment service. <c>OwnerName</c> is set only for flights owned by someone else.
/// </summary>
public record FlightListItemDto(
    Guid Id,
    string Title,
    DateOnly FlightDate,
    TimeOnly FlightTime,
    string? BalloonRegistration,
    string? LocationName,
    bool IsFlown,
    DateTime? FlownAt,
    string GoNoGoVerdict,
    bool IsShared,
    string? OwnerName);

public record FlightListPageDto(IReadOnlyList<FlightListItemDto> Items, int TotalCount, int Page, int PageSize);
