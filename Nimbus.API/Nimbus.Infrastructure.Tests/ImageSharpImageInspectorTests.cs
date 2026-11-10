using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nimbus.Application.Features.FlightPreparations.Images;
using Nimbus.Infrastructure.Imaging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.PixelFormats;
namespace Nimbus.Infrastructure.Tests;

/// <summary>Issue #51: the file's bytes decide whether it is an image, and list views get a smaller variant.</summary>
[TestFixture]
public class ImageSharpImageInspectorTests
{
    private static ImageSharpImageInspector Inspector(ImageUploadOptions? options = null) =>
        new(Options.Create(options ?? new ImageUploadOptions()), NullLogger<ImageSharpImageInspector>.Instance);

    private static byte[] Encode(int width, int height, Func<Stream, Image<Rgba32>, Task> save)
    {
        using var image = new Image<Rgba32>(width, height, new Rgba32(10, 120, 200));
        using var ms = new MemoryStream();
        save(ms, image).GetAwaiter().GetResult();
        return ms.ToArray();
    }

    [Test]
    public async Task APng_IsDetectedFromItsBytes_AndGetsASmallerThumbnailWithinTheConfiguredEdge()
    {
        var png = Encode(2000, 1000, (s, i) => i.SaveAsPngAsync(s));

        var result = await Inspector().InspectAsync(png);

        Assert.That(result, Is.Not.Null);
        Assert.That((result!.ContentType, result.Extension), Is.EqualTo(("image/png", "png")));
        Assert.That(result.Thumbnail.Content.Length, Is.LessThan(png.Length));
        using var thumb = Image.Load(result.Thumbnail.Content);
        Assert.That((thumb.Width, thumb.Height), Is.EqualTo((480, 240)));
    }

    [Test]
    public async Task AJpegAndAWebp_AreDetectedToo()
    {
        var jpg = await Inspector().InspectAsync(Encode(64, 64, (s, i) => i.SaveAsJpegAsync(s)));
        var webp = await Inspector().InspectAsync(Encode(64, 64, (s, i) => i.SaveAsWebpAsync(s)));

        Assert.That(jpg?.ContentType, Is.EqualTo("image/jpeg"));
        Assert.That(webp?.ContentType, Is.EqualTo("image/webp"));
    }

    [Test]
    public async Task ASmallImage_IsNotUpscaled()
    {
        var result = await Inspector().InspectAsync(Encode(100, 50, (s, i) => i.SaveAsPngAsync(s)));

        using var thumb = Image.Load(result!.Thumbnail.Content);
        Assert.That((thumb.Width, thumb.Height), Is.EqualTo((100, 50)));
    }

    [Test]
    public async Task ATextFile_NamedLikeAnImage_IsRejected()
    {
        Assert.That(await Inspector().InspectAsync("<html>not an image</html>"u8.ToArray()), Is.Null);
    }

    [Test]
    public async Task AValidImageThatIsTruncated_IsRejected()
    {
        var png = Encode(300, 300, (s, i) => i.SaveAsPngAsync(s));
        Assert.That(await Inspector().InspectAsync(png[..(png.Length / 2)]), Is.Null);
    }

    [Test]
    public async Task AFormatOutsideTheSupportedSet_IsRejected()
    {
        Assert.That(await Inspector().InspectAsync(Encode(8, 8, (s, i) => i.SaveAsGifAsync(s))), Is.Null);
    }

    [Test]
    public async Task AnImageBeyondTheEdgeCeiling_IsRejectedBeforeItIsDecoded()
    {
        var png = Encode(300, 10, (s, i) => i.SaveAsPngAsync(s));
        Assert.That(await Inspector(new ImageUploadOptions { MaxEdgePx = 200 }).InspectAsync(png), Is.Null);
    }

    [Test]
    public async Task TheThumbnail_CarriesNoMetadata()
    {
        var jpg = Encode(64, 64, (s, i) =>
        {
            i.Metadata.ExifProfile = new SixLabors.ImageSharp.Metadata.Profiles.Exif.ExifProfile();
            i.Metadata.ExifProfile.SetValue(SixLabors.ImageSharp.Metadata.Profiles.Exif.ExifTag.Software, "secret-camera");
            return i.SaveAsJpegAsync(s);
        });

        var result = await Inspector().InspectAsync(jpg);

        Assert.That(System.Text.Encoding.ASCII.GetString(result!.Thumbnail.Content), Does.Not.Contain("secret-camera"));
    }
}
