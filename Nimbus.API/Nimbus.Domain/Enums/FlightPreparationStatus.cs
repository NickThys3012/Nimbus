namespace Nimbus.Domain.Enums;

/// <summary>The lifecycle of a flight preparation dossier (issue #31).</summary>
public enum FlightPreparationStatus
{
    /// <summary>The flight has not happened yet; the dossier only carries the pre-flight sections.</summary>
    Planned,

    /// <summary>The flight has happened; the flight-outcome fields ("mark as flown", issue #68) are populated.</summary>
    Flown
}
