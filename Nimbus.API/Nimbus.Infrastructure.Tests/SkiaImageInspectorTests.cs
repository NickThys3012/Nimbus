using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nimbus.Application.Features.FlightPreparations.Images;
using Nimbus.Infrastructure.Imaging;
using SkiaSharp;
namespace Nimbus.Infrastructure.Tests;

/// <summary>Issue #51: the file's bytes decide whether it is an image, and list views get a smaller variant.</summary>
[TestFixture]
public class SkiaImageInspectorTests
{
    private static SkiaImageInspector Inspector(ImageUploadOptions? options = null) =>
        new(Options.Create(options ?? new ImageUploadOptions()), NullLogger<SkiaImageInspector>.Instance);

    private static byte[] Encode(int width, int height, SKEncodedImageFormat format)
    {
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(new SKColor(10, 120, 200));
        using var image = SKImage.FromBitmap(bitmap);
        return image.Encode(format, 90).ToArray();
    }

    private static (int Width, int Height) SizeOf(byte[] bytes)
    {
        using var bitmap = SKBitmap.Decode(bytes);
        return (bitmap.Width, bitmap.Height);
    }

    /// <summary>Inserts raw JPEG segments straight after the SOI marker.</summary>
    private static byte[] WithSegments(byte[] jpeg, params byte[][] segments) =>
        [.. jpeg[..2], .. segments.SelectMany(s => s), .. jpeg[2..]];

    private static byte[] Segment(byte marker, byte[] payload) =>
        [0xFF, marker, (byte)((payload.Length + 2) >> 8), (byte)((payload.Length + 2) & 0xFF), .. payload];

    /// <summary>A minimal EXIF block that only sets the orientation tag.</summary>
    private static byte[] ExifOrientation(int orientation) =>
        Segment(0xE1, [.. "Exif\0\0"u8.ToArray(), 0x49, 0x49, 0x2A, 0, 8, 0, 0, 0, 1, 0, 0x12, 0x01, 3, 0, 1, 0, 0, 0, (byte)orientation, 0, 0, 0, 0, 0, 0, 0]);

    [Test]
    public async Task APng_IsDetectedFromItsBytes_AndGetsASmallerThumbnailWithinTheConfiguredEdge()
    {
        var png = Encode(2000, 1000, SKEncodedImageFormat.Png);

        var result = await Inspector().InspectAsync(png);

        Assert.That(result, Is.Not.Null);
        Assert.That((result!.ContentType, result.Extension), Is.EqualTo(("image/png", "png")));
        Assert.That(result.Thumbnail.ContentType, Is.EqualTo("image/png"));
        Assert.That(result.Thumbnail.Content.Length, Is.LessThan(png.Length));
        Assert.That(SizeOf(result.Thumbnail.Content), Is.EqualTo((480, 240)));
    }

    [Test]
    public async Task AJpegAndAWebp_AreDetectedToo()
    {
        var jpg = await Inspector().InspectAsync(Encode(64, 64, SKEncodedImageFormat.Jpeg));
        var webp = await Inspector().InspectAsync(Encode(64, 64, SKEncodedImageFormat.Webp));

        Assert.That((jpg?.ContentType, jpg?.Thumbnail.ContentType), Is.EqualTo(("image/jpeg", "image/jpeg")));
        Assert.That(webp?.ContentType, Is.EqualTo("image/webp"));
    }

    [Test]
    public async Task ASmallImage_IsNotUpscaled()
    {
        var result = await Inspector().InspectAsync(Encode(100, 50, SKEncodedImageFormat.Png));

        Assert.That(SizeOf(result!.Thumbnail.Content), Is.EqualTo((100, 50)));
    }

    [Test]
    public async Task ATextFile_NamedLikeAnImage_IsRejected()
    {
        Assert.That(await Inspector().InspectAsync("<html>not an image</html>"u8.ToArray()), Is.Null);
    }

    [Test]
    public async Task AValidImageThatIsTruncated_IsRejected()
    {
        var png = Encode(300, 300, SKEncodedImageFormat.Png);
        Assert.That(await Inspector().InspectAsync(png[..(png.Length / 2)]), Is.Null);
    }

    [Test]
    public async Task AFormatOutsideTheSupportedSet_IsRejected()
    {
        // A valid 1x1 GIF.
        byte[] gif = [0x47, 0x49, 0x46, 0x38, 0x39, 0x61, 1, 0, 1, 0, 0x80, 0, 0, 0, 0, 0, 0xFF, 0xFF, 0xFF, 0x21, 0xF9, 4, 1, 0, 0, 0, 0, 0x2C, 0, 0, 0, 0, 1, 0, 1, 0, 0, 2, 2, 0x44, 1, 0, 0x3B];
        Assert.That(await Inspector().InspectAsync(gif), Is.Null);
    }

    [Test]
    public async Task AnImageBeyondTheEdgeCeiling_IsRejectedBeforeItIsDecoded()
    {
        var png = Encode(300, 10, SKEncodedImageFormat.Png);
        Assert.That(await Inspector(new ImageUploadOptions { MaxEdgePx = 200 }).InspectAsync(png), Is.Null);
    }

    [Test]
    public async Task TheThumbnail_CarriesNoMetadata()
    {
        var jpg = WithSegments(Encode(64, 64, SKEncodedImageFormat.Jpeg), Segment(0xFE, Encoding.ASCII.GetBytes("secret-camera")));
        Assert.That(Encoding.ASCII.GetString(jpg), Does.Contain("secret-camera"), "the fixture does carry the marker");

        var result = await Inspector().InspectAsync(jpg);

        Assert.That(Encoding.ASCII.GetString(result!.Thumbnail.Content), Does.Not.Contain("secret-camera"));
    }

    [Test]
    public async Task TheThumbnail_IsRotatedUpright_FromTheExifOrientation()
    {
        // Stored 100x50 but flagged "rotate 90° clockwise to view": upright it is 50x100.
        var jpg = WithSegments(Encode(100, 50, SKEncodedImageFormat.Jpeg), ExifOrientation(6));

        var result = await Inspector().InspectAsync(jpg);

        Assert.That(SizeOf(result!.Thumbnail.Content), Is.EqualTo((50, 100)));
    }
}
