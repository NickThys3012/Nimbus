using Nimbus.Domain.Exceptions;
namespace Nimbus.Domain.ValueObjects;

/// <summary>
///     A validated object-storage key.
/// </summary>
/// <remarks>
///     <para><b>Convention</b></para>
///     <para>
///     Every object stored in an owner/flight-scoped bucket (<c>flight-images</c>,
///     <c>flight-tracks</c>, <c>flight-exports</c>) uses the key shape:
///     </para>
///     <code>{ownerId}/{flightId}/{fileName}</code>
///     <para>
///     This means any object's owner and originating flight can be recovered from its key alone,
///     without a database lookup — so an object whose owner/flight no longer exists (an orphan,
///     e.g. after a flight is deleted but the delete of its objects failed) is identifiable purely
///     by listing a bucket and checking each prefix against the database.
///     </para>
///     <para>
///     <c>map-cache</c> is not owner/flight scoped — it holds shared, content-addressable tiles
///     that outlive any single flight — so it uses <see cref="ForSharedAsset" /> instead.
///     </para>
/// </remarks>
public sealed class ObjectKey
{
    private ObjectKey(string value)
    {
        Value = value;
    }

    public string Value { get; }

    /// <summary>
    ///     Builds the key for an object that belongs to a specific owner and flight
    ///     (<c>flight-images</c>, <c>flight-tracks</c>, <c>flight-exports</c>).
    /// </summary>
    public static ObjectKey ForFlightAsset(Guid ownerId, Guid flightId, string fileName)
    {
        if (ownerId == Guid.Empty)
        {
            throw new DomainException("Object key requires a non-empty owner id.");
        }

        if (flightId == Guid.Empty)
        {
            throw new DomainException("Object key requires a non-empty flight id.");
        }

        ValidateFileName(fileName);

        return new ObjectKey($"{ownerId:D}/{flightId:D}/{fileName}");
    }

    /// <summary>
    ///     Builds the key for a shared, non owner/flight-scoped asset (<c>map-cache</c>).
    /// </summary>
    public static ObjectKey ForSharedAsset(string relativePath)
    {
        ValidateFileName(relativePath);
        return new ObjectKey(relativePath);
    }

    private static void ValidateFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new DomainException("Object key requires a non-empty file name.");
        }

        if (fileName.Contains("..", StringComparison.Ordinal) || fileName.StartsWith('/'))
        {
            throw new DomainException("Object key file name must not contain path traversal segments.");
        }
    }

    public override string ToString()
    {
        return Value;
    }
}
