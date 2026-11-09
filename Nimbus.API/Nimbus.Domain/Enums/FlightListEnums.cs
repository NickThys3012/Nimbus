namespace Nimbus.Domain.Enums;

/// <summary>Which of the flights visible to the caller the list returns (issue #40).</summary>
public enum FlightListFilter
{
    All,
    Flown,
    NotFlown,

    /// <summary>Flights owned by someone else that are visible to the caller through a share (or, for an admin, any foreign flight).</summary>
    SharedWithMe
}

/// <summary>Columns the flight list can be ordered by; always translated to SQL ORDER BY.</summary>
public enum FlightListSort
{
    Date,
    Balloon,
    Location,
    Status
}
