using Nimbus.Domain.Entities.Base;
using Nimbus.Domain.Enums;
namespace Nimbus.Domain.Entities;

/// <summary>
///     An image attached to one section of a flight preparation (issue #31). Stores an
///     object-storage reference only — never file bytes (see docs/object-storage.md) — tagged
///     with the section it belongs to and a display order. Cascade-deletes with the parent
///     <see cref="FlightPreparation" />. The row is metadata only (object name, section, order, content type,
///     size; <c>CreatedDate</c> is the upload time); the bytes live in the <c>flight-images</c> bucket (issue #51).
/// </summary>
public class FlightPreparationImage : BaseEntity
{
    public Guid FlightPreparationId { get; set; }
    public FlightPreparation FlightPreparation { get; set; } = null!;

    public FlightPreparationSection Section { get; set; }

    /// <summary>Object-storage key (<c>Nimbus.Domain.ValueObjects.ObjectKey</c>), not file content.</summary>
    public string ObjectKey { get; set; } = "";

    /// <summary>The content type detected from the file's own bytes at upload, never the client's claim.</summary>
    public string ContentType { get; set; } = "";

    /// <summary>Size of the stored original, in bytes.</summary>
    public long SizeBytes { get; set; }

    /// <summary>Object-storage key of the resized variant used by list views; null for rows that predate thumbnails.</summary>
    public string? ThumbnailObjectKey { get; set; }

    /// <summary>Display order within its section; part of the <c>(FlightPreparationId, Section, Order)</c> index.</summary>
    public int Order { get; set; }
}
