using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nimbus.Application.Common.Interfaces;
using Nimbus.Application.Features.FlightPreparations.Images;
using SkiaSharp;
namespace Nimbus.Infrastructure.Imaging;

/// <summary>
///     <see cref="IImageInspector" /> over SkiaSharp (MIT). The format comes from the file's magic bytes and the file is then
///     decoded in full, so a renamed text file, a truncated file or a payload that only starts like an image is rejected. Only
///     JPEG, PNG and WebP are accepted. Dimensions are read from the header first and refused above the configured edge
///     length, before any pixel memory is allocated. Thumbnails are re-encoded from pixels, so they carry none of the
///     original's metadata (EXIF/GPS), and are rotated upright according to the original's EXIF orientation.
/// </summary>
public sealed class SkiaImageInspector : IImageInspector
{
    private readonly ILogger<SkiaImageInspector> _logger;
    private readonly ImageUploadOptions _options;

    public SkiaImageInspector(IOptions<ImageUploadOptions> options, ILogger<SkiaImageInspector> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public Task<InspectedImage?> InspectAsync(byte[] content, CancellationToken ct = default)
    {
        // Decoding is CPU-bound and synchronous; keep it off the request thread.
        return Task.Run(() => Inspect(content), ct);
    }

    private InspectedImage? Inspect(byte[] content)
    {
        using var stream = new SKMemoryStream(content);
        using var codec = SKCodec.Create(stream, out var created);
        if (created != SKCodecResult.Success)
            return Rejected($"no decoder could read the bytes ({created})");

        (string ContentType, string Extension, SKEncodedImageFormat Format)? detected = codec.EncodedFormat switch
        {
            SKEncodedImageFormat.Jpeg => ("image/jpeg", "jpg", SKEncodedImageFormat.Jpeg),
            SKEncodedImageFormat.Png => ("image/png", "png", SKEncodedImageFormat.Png),
            SKEncodedImageFormat.Webp => ("image/webp", "webp", SKEncodedImageFormat.Webp),
            _ => null
        };
        if (detected is not var (contentType, extension, format))
            return Rejected($"unsupported format {codec.EncodedFormat}");

        var info = codec.Info;
        if (info.Width <= 0 || info.Height <= 0 || Math.Max(info.Width, info.Height) > _options.MaxEdgePx)
            return Rejected($"dimensions {info.Width}x{info.Height} are outside the limit");

        using var decoded = new SKBitmap(info.WithAlphaType(SKAlphaType.Premul));
        var result = codec.GetPixels(decoded.Info, decoded.GetPixels());
        if (result != SKCodecResult.Success)
            return Rejected($"decoding failed ({result})");

        using var upright = Orient(decoded, codec.EncodedOrigin);
        using var thumbnail = Shrink(upright);
        // Thumbnails keep PNG for PNG sources (charts, possibly transparent) and are JPEG otherwise.
        var thumbFormat = format == SKEncodedImageFormat.Png ? SKEncodedImageFormat.Png : SKEncodedImageFormat.Jpeg;
        using var image = SKImage.FromBitmap(thumbnail);
        using var encoded = image.Encode(thumbFormat, 80);
        if (encoded is null)
            return Rejected("the thumbnail could not be encoded");

        return new InspectedImage(contentType, extension, new ImageVariant(encoded.ToArray(), thumbFormat == SKEncodedImageFormat.Png ? "image/png" : "image/jpeg"));
    }

    private InspectedImage? Rejected(string reason)
    {
        _logger.LogInformation("Rejected a file that is not a supported image: {Reason}", reason);
        return null;
    }

    /// <summary>Only ever shrinks: a small chart is not blown up (and made heavier) for a list view.</summary>
    private SKBitmap Shrink(SKBitmap source)
    {
        var longest = Math.Max(source.Width, source.Height);
        if (longest <= _options.ThumbnailMaxEdgePx)
            return source.Copy();

        var scale = (double)_options.ThumbnailMaxEdgePx / longest;
        var target = new SKImageInfo(Math.Max(1, (int)Math.Round(source.Width * scale)), Math.Max(1, (int)Math.Round(source.Height * scale)), source.ColorType, source.AlphaType);
        return source.Resize(target, new SKSamplingOptions(SKCubicResampler.Mitchell))
               ?? throw new InvalidOperationException("Resizing the image failed.");
    }

    /// <summary>Applies the EXIF orientation so the thumbnail is upright (the original is stored untouched).</summary>
    private static SKBitmap Orient(SKBitmap source, SKEncodedOrigin origin)
    {
        if (origin is SKEncodedOrigin.TopLeft or SKEncodedOrigin.Default)
            return source.Copy();

        var w = source.Width;
        var h = source.Height;
        var swaps = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        // Each matrix maps a source pixel (x, y) to its upright position.
        var m = origin switch
        {
            SKEncodedOrigin.TopRight => Matrix(-1, 0, w, 0, 1, 0),
            SKEncodedOrigin.BottomRight => Matrix(-1, 0, w, 0, -1, h),
            SKEncodedOrigin.BottomLeft => Matrix(1, 0, 0, 0, -1, h),
            SKEncodedOrigin.LeftTop => Matrix(0, 1, 0, 1, 0, 0),
            SKEncodedOrigin.RightTop => Matrix(0, -1, h, 1, 0, 0),
            SKEncodedOrigin.RightBottom => Matrix(0, -1, h, -1, 0, w),
            SKEncodedOrigin.LeftBottom => Matrix(0, 1, 0, -1, 0, w),
            _ => SKMatrix.Identity
        };

        var result = new SKBitmap(swaps ? h : w, swaps ? w : h, source.ColorType, source.AlphaType);
        using var canvas = new SKCanvas(result);
        canvas.SetMatrix(m);
        canvas.DrawBitmap(source, 0, 0);
        return result;

        static SKMatrix Matrix(float sx, float kx, float tx, float ky, float sy, float ty)
        {
            return new SKMatrix(sx, kx, tx, ky, sy, ty, 0, 0, 1);
        }
    }
}
