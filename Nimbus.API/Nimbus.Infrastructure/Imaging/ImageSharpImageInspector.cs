using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nimbus.Application.Common.Interfaces;
using Nimbus.Application.Features.FlightPreparations.Images;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;
namespace Nimbus.Infrastructure.Imaging;

/// <summary>
///     <see cref="IImageInspector" /> over ImageSharp. The format is detected from the file's magic bytes and the file is then
///     decoded in full, so a renamed text file, a truncated file or a payload that only starts like an image is rejected. Only
///     JPEG, PNG and WebP are ever decoded. Dimensions are read from the header first and refused above the configured edge
///     length, before any pixel memory is allocated. Thumbnails carry no metadata (no EXIF/GPS) and are rotated upright.
/// </summary>
public sealed class ImageSharpImageInspector : IImageInspector
{
    private static readonly Configuration SupportedFormats = new(new JpegConfigurationModule(), new PngConfigurationModule(), new WebpConfigurationModule());

    private readonly ILogger<ImageSharpImageInspector> _logger;
    private readonly ImageUploadOptions _options;

    public ImageSharpImageInspector(IOptions<ImageUploadOptions> options, ILogger<ImageSharpImageInspector> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<InspectedImage?> InspectAsync(byte[] content, CancellationToken ct = default)
    {
        var decoder = new DecoderOptions { Configuration = SupportedFormats };
        try
        {
            var info = await Image.IdentifyAsync(decoder, new MemoryStream(content, false), ct);
            if (info is null || Math.Max(info.Width, info.Height) > _options.MaxEdgePx)
                return null;

            using var image = await Image.LoadAsync(decoder, new MemoryStream(content, false), ct);
            var format = image.Metadata.DecodedImageFormat;
            (string ContentType, string Extension)? detected = format switch
            {
                JpegFormat => ("image/jpeg", "jpg"),
                PngFormat => ("image/png", "png"),
                WebpFormat => ("image/webp", "webp"),
                _ => null
            };
            if (detected is not var (contentType, extension))
                return null;

            image.Mutate(x => x.AutoOrient());
            if (Math.Max(image.Width, image.Height) > _options.ThumbnailMaxEdgePx)
            {
                // Only ever shrinks: a small chart is not blown up (and made heavier) for a list view.
                image.Mutate(x => x.Resize(new ResizeOptions
                {
                    Mode = ResizeMode.Max, Size = new Size(_options.ThumbnailMaxEdgePx, _options.ThumbnailMaxEdgePx), Sampler = KnownResamplers.Lanczos3
                }));
            }

            // The thumbnail never carries the original's camera, GPS or colour-profile metadata.
            image.Metadata.ExifProfile = null;
            image.Metadata.IccProfile = null;
            image.Metadata.IptcProfile = null;
            image.Metadata.XmpProfile = null;

            using var output = new MemoryStream();
            ImageEncoder encoder = format is PngFormat
                ? new PngEncoder { SkipMetadata = true }
                : new JpegEncoder { SkipMetadata = true, Quality = 80 };
            await image.SaveAsync(output, encoder, ct);
            return new InspectedImage(contentType, extension, new ImageVariant(output.ToArray(), format is PngFormat ? "image/png" : "image/jpeg"));
        }
        catch (Exception ex) when (ex is UnknownImageFormatException or InvalidImageContentException or ImageFormatException or NotSupportedException or InvalidOperationException or EndOfStreamException)
        {
            _logger.LogInformation(ex, "Rejected a file that does not decode as a supported image");
            return null;
        }
    }
}
