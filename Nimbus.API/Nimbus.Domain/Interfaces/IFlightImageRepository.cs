using Nimbus.Domain.Entities;
namespace Nimbus.Domain.Interfaces;

/// <summary>
///     Persistence for the image metadata rows of a flight (issue #51). Rows only — the files are the object store's.
///     Callers check the viewer's access to the flight first; these methods match on flight id.
/// </summary>
public interface IFlightImageRepository
{
    /// <summary>Appends the image to the end of its section (order = highest existing + 1) and saves it.</summary>
    Task AddAsync(FlightPreparationImage image, CancellationToken ct = default);

    /// <summary>The image of this flight, or null when there is none (a soft-deleted row counts as none).</summary>
    Task<FlightPreparationImage?> GetAsync(Guid flightId, Guid imageId, CancellationToken ct = default);

    /// <summary>Every image of the flight, soft-deleted rows excluded, ordered by section then order.</summary>
    Task<IReadOnlyList<FlightPreparationImage>> ListAsync(Guid flightId, CancellationToken ct = default);

    /// <summary>Sets <c>Order</c> to the position in <paramref name="orderedImageIds" /> for those images of the flight, in one save.</summary>
    Task SetOrderAsync(Guid flightId, IReadOnlyList<Guid> orderedImageIds, CancellationToken ct = default);

    /// <summary>Removes the row. False when it was already gone.</summary>
    Task<bool> DeleteAsync(Guid flightId, Guid imageId, CancellationToken ct = default);
}
