namespace Nimbus.Domain.Enums;

/// <summary>
///     The logical object-storage buckets the application uses. Every bucket is configured
///     (never hard-coded as a raw string) so the Infrastructure implementation resolves the
///     real bucket name for each value — see <c>IObjectStorageService</c>.
/// </summary>
public enum StorageBucket
{
    /// <summary>User-uploaded flight photos and other imagery.</summary>
    FlightImages,

    /// <summary>Raw and processed GPS/track data for a flight.</summary>
    FlightTracks,

    /// <summary>Generated exports (e.g. PDF/GPX) derived from flight data.</summary>
    FlightExports,

    /// <summary>Cached map tiles/imagery shared across flights and owners.</summary>
    MapCache
}
