using Nimbus.Domain.Entities;
namespace Nimbus.Domain.Interfaces;

/// <summary>
///     Persistence for the flight-preparation dossier (issue #42). Every method takes the owner id
///     and combines it with the flight id; a flight of another user is indistinguishable from a
///     missing one. See docs/flight-preparation.md ("Saving") for the transaction, detach/restore
///     and child-collection strategy.
/// </summary>
/// <summary>The object-storage references of a flight that has just been deleted, so the caller can remove the files too.</summary>
public sealed record FlightDeletion(IReadOnlyList<string> ImageObjectKeys, string? KmlTrackObjectKey);

public interface IFlightPreparationRepository
{
    /// <summary>The flight with its passengers and wind levels (ordered), or null when it is not this owner's.</summary>
    Task<FlightPreparation?> GetByIdAsync(Guid ownerId, Guid id, CancellationToken ct = default);

    /// <summary>
    ///     Inserts the flight, then its passengers and wind levels, in one transaction. All-or-nothing:
    ///     on failure nothing is stored and the caller's object graph is left exactly as passed in.
    /// </summary>
    Task AddAsync(FlightPreparation flight, CancellationToken ct = default);

    /// <summary>
    ///     Updates the scalar columns and replaces passengers and wind levels with the supplied lists,
    ///     in one transaction. Returns false (nothing changed) when the flight is not this owner's.
    ///     Images, shares, the KML reference and the trajectory JSON are not touched.
    /// </summary>
    Task<bool> UpdateAsync(Guid ownerId, FlightPreparation flight, CancellationToken ct = default);

    /// <summary>Single-column UPDATE of the trajectory JSON without loading the row. False when not this owner's.</summary>
    /// <summary>
    ///     Permanently deletes the flight (a hard delete, not a soft one) together with its passengers, wind levels, images and
    ///     shares, which the database removes by cascade, soft-deleted rows included. Matches on id and owner; another user's
    ///     flight behaves like a missing one (null). Returns the object-storage keys the flight referenced; the files are not
    ///     touched here.
    /// </summary>
    Task<FlightDeletion?> DeleteAsync(Guid ownerId, Guid id, CancellationToken ct = default);

    Task<bool> SetTrajectoryJsonAsync(Guid ownerId, Guid id, string? trajectoryJson, CancellationToken ct = default);

    /// <summary>Single-statement UPDATE of the KML reference (key and content type) without loading the row. False when not this owner's.</summary>
    Task<bool> SetKmlTrackAsync(Guid ownerId, Guid id, string? objectKey, string? contentType, CancellationToken ct = default);
}
