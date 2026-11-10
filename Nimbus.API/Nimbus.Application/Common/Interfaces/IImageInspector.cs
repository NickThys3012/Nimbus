namespace Nimbus.Application.Common.Interfaces;

/// <summary>A generated image file: its bytes and the content type they are encoded as.</summary>
public sealed record ImageVariant(byte[] Content, string ContentType);

/// <summary>
///     What the file really is, as found by decoding its bytes. <see cref="ContentType" /> and <see cref="Extension" />
///     come from the detected format, never from the client's claim or file name.
/// </summary>
public sealed record InspectedImage(string ContentType, string Extension, ImageVariant Thumbnail);

/// <summary>
///     Verifies that bytes are an image of a supported format and produces the resized variant used by list views, in one
///     decode (issue #51). Implemented in Infrastructure so the imaging library stays out of the application layer.
/// </summary>
public interface IImageInspector
{
    /// <summary>
    ///     Null when the bytes are not a supported image: unknown or unsupported format, corrupt data, or dimensions beyond the
    ///     configured ceiling (which guards against decompression bombs).
    /// </summary>
    Task<InspectedImage?> InspectAsync(byte[] content, CancellationToken ct = default);
}
