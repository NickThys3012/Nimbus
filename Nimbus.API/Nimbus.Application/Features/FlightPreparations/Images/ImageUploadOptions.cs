namespace Nimbus.Application.Features.FlightPreparations.Images;

/// <summary>Limits for the images attached to a flight (bound from the <c>ImageUpload</c> config section).</summary>
public sealed class ImageUploadOptions
{
    public const string SectionName = "ImageUpload";

    /// <summary>Largest accepted upload, in bytes. Enforced while reading, so a lying Content-Length does not get past it.</summary>
    public long MaxBytes { get; init; } = 10 * 1024 * 1024;

    /// <summary>Content types accepted for upload. The file's own bytes must agree with whichever one it claims.</summary>
    public string[] AllowedContentTypes { get; init; } = ["image/jpeg", "image/png", "image/webp"];

    /// <summary>Longest edge of the list-view variant, in pixels.</summary>
    public int ThumbnailMaxEdgePx { get; init; } = 480;

    /// <summary>Longest edge accepted for an upload, in pixels — a tiny file can otherwise decode into gigabytes.</summary>
    public int MaxEdgePx { get; init; } = 12000;
}
