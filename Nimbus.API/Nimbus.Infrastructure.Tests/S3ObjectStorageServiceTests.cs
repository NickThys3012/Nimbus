using System.Text;
using Amazon.Runtime;
using Amazon.S3;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nimbus.Domain.Enums;
using Nimbus.Domain.Exceptions;
using Nimbus.Domain.Interfaces;
using Nimbus.Domain.ValueObjects;
using Nimbus.Infrastructure.Storage;
using Testcontainers.Minio;
namespace Nimbus.Infrastructure.Tests;

/// <summary>
///     Integration tests for <see cref="S3ObjectStorageService" /> against a real MinIO container
///     (issue #11's requirement that these run against real MinIO, not a mock).
/// </summary>
[TestFixture]
public class S3ObjectStorageServiceTests
{
    private static readonly HttpClient HttpClient = new();
    private const string BucketImages = "flight-images";
    private const string BucketTracks = "flight-tracks";

    private MinioContainer _minio = null!;
    private IAmazonS3 _adminClient = null!;
    private IObjectStorageService _sut = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUpAsync()
    {
        _minio = new MinioBuilder("minio/minio:latest")
            .Build();

        await _minio.StartAsync();

        var config = new AmazonS3Config
        {
            ServiceURL = _minio.GetConnectionString(),
            ForcePathStyle = true,
            UseHttp = true
        };

        var credentials = new BasicAWSCredentials(_minio.GetAccessKey(), _minio.GetSecretKey());
        _adminClient = new AmazonS3Client(credentials, config);

        foreach (var bucket in new[] { BucketImages, BucketTracks, "flight-exports", "map-cache" })
        {
            await _adminClient.PutBucketAsync(bucket);
        }

        var options = Options.Create(new StorageOptions
        {
            Endpoint = _minio.GetConnectionString(),
            AccessKey = _minio.GetAccessKey(),
            SecretKey = _minio.GetSecretKey(),
            ForcePathStyle = true,
            UseHttps = false,
            PresignedUrlExpiry = TimeSpan.FromMinutes(5),
            MaxRetryAttempts = 2,
            Buckets = new StorageBucketNames
            {
                FlightImages = BucketImages,
                FlightTracks = BucketTracks,
                FlightExports = "flight-exports",
                MapCache = "map-cache"
            }
        });

        _sut = new S3ObjectStorageService(_adminClient, options, NullLogger<S3ObjectStorageService>.Instance);
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDownAsync()
    {
        _adminClient.Dispose();
        await _minio.DisposeAsync();
    }

    private static ObjectKey NewKey(string fileName = "photo.jpg")
    {
        return ObjectKey.ForFlightAsset(Guid.NewGuid(), Guid.NewGuid(), fileName);
    }

    [Test]
    public async Task UploadAsync_then_DownloadAsync_round_trips_content_type_and_bytes()
    {
        var key = NewKey();
        var bytes = Encoding.UTF8.GetBytes("hello nimbus");

        await using (var content = new MemoryStream(bytes))
        {
            await _sut.UploadAsync(StorageBucket.FlightImages, key, new ObjectUpload(content, "image/jpeg", bytes.Length));
        }

        await using var download = await _sut.DownloadAsync(StorageBucket.FlightImages, key);

        Assert.That(download, Is.Not.Null);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(download!.ContentType, Is.EqualTo("image/jpeg"));
            Assert.That(download.ContentLength, Is.EqualTo(bytes.Length));
        }

        using var reader = new StreamReader(download.Content);
        var text = await reader.ReadToEndAsync();
        Assert.That(text, Is.EqualTo("hello nimbus"));
    }

    [Test]
    public async Task DownloadAsync_returns_null_for_missing_key()
    {
        var result = await _sut.DownloadAsync(StorageBucket.FlightImages, NewKey("missing.jpg"));

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task ExistsAsync_reflects_upload_and_delete()
    {
        var key = NewKey("track.gpx");
        var bytes = "<gpx/>"u8.ToArray();

        Assert.That(await _sut.ExistsAsync(StorageBucket.FlightTracks, key), Is.False);

        await using (var content = new MemoryStream(bytes))
        {
            await _sut.UploadAsync(StorageBucket.FlightTracks, key, new ObjectUpload(content, "application/gpx+xml", bytes.Length));
        }

        Assert.That(await _sut.ExistsAsync(StorageBucket.FlightTracks, key), Is.True);

        await _sut.DeleteAsync(StorageBucket.FlightTracks, key);

        Assert.That(await _sut.ExistsAsync(StorageBucket.FlightTracks, key), Is.False);
    }

    [Test]
    public void DeleteAsync_is_idempotent_for_a_missing_key()
    {
        Assert.DoesNotThrowAsync(async () => await _sut.DeleteAsync(StorageBucket.FlightImages, NewKey("never-uploaded.jpg")));
    }

    [Test]
    public async Task GetPresignedDownloadUrlAsync_returns_a_working_short_lived_url()
    {
        var key = NewKey("presigned.jpg");
        var bytes = "presigned-content"u8.ToArray();

        await using (var content = new MemoryStream(bytes))
        {
            await _sut.UploadAsync(StorageBucket.FlightImages, key, new ObjectUpload(content, "image/jpeg", bytes.Length));
        }

        var url = await _sut.GetPresignedDownloadUrlAsync(StorageBucket.FlightImages, key);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(url.IsAbsoluteUri, Is.True);
            Assert.That(url.Query, Does.Contain("X-Amz-Expires"));
        }

        var response = await HttpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();
        var downloaded = await response.Content.ReadAsStringAsync();
        Assert.That(downloaded, Is.EqualTo("presigned-content"));
    }

    [Test]
    public void DownloadAsync_with_invalid_credentials_throws_ObjectStorageException()
    {
        // A credentials/auth failure (403) is not treated as "not found" — unlike a missing
        // bucket/key, which DownloadAsync legitimately maps to a null result — so this should
        // surface as a handled ObjectStorageException once retries are exhausted.
        var brokenOptions = Options.Create(new StorageOptions
        {
            Endpoint = _minio.GetConnectionString(),
            AccessKey = _minio.GetAccessKey(),
            SecretKey = "wrong-secret-key",
            ForcePathStyle = true,
            UseHttps = false,
            MaxRetryAttempts = 1,
            Buckets = new StorageBucketNames { FlightImages = BucketImages }
        });

        var config = new AmazonS3Config { ServiceURL = _minio.GetConnectionString(), ForcePathStyle = true, UseHttp = true };
        using var client = new AmazonS3Client(new BasicAWSCredentials(_minio.GetAccessKey(), "wrong-secret-key"), config);
        var service = new S3ObjectStorageService(client, brokenOptions, NullLogger<S3ObjectStorageService>.Instance);

        Assert.ThrowsAsync<ObjectStorageException>(async () => await service.DownloadAsync(StorageBucket.FlightImages, NewKey()));
    }
}
