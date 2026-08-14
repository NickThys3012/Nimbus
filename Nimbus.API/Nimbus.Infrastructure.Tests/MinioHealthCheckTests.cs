using Amazon.Runtime;
using Amazon.S3;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Nimbus.Infrastructure.Storage;
using Testcontainers.Minio;
namespace Nimbus.Infrastructure.Tests;

/// <summary>
///     Tests for <see cref="MinioHealthCheck" /> against a real MinIO container (issue #97) —
///     this is what <c>/health/ready</c> gates on, so it must actually flip unhealthy when MinIO
///     stops, not just when the SDK call is mocked to throw.
/// </summary>
[TestFixture]
public class MinioHealthCheckTests
{
    private MinioContainer _minio = null!;

    [SetUp]
    public async Task SetUpAsync()
    {
        _minio = new MinioBuilder("minio/minio:latest").Build();
        await _minio.StartAsync();
    }

    [TearDown]
    public async Task TearDownAsync()
    {
        await _minio.DisposeAsync();
    }

    private IAmazonS3 CreateClient()
    {
        var config = new AmazonS3Config
        {
            ServiceURL = _minio.GetConnectionString(), ForcePathStyle = true, UseHttp = true
        };
        var credentials = new BasicAWSCredentials(_minio.GetAccessKey(), _minio.GetSecretKey());
        return new AmazonS3Client(credentials, config);
    }

    [Test]
    public async Task CheckHealthAsync_ReturnsHealthy_WhenMinioIsReachable()
    {
        using var client = CreateClient();
        var sut = new MinioHealthCheck(client);

        var result = await sut.CheckHealthAsync(new HealthCheckContext());

        Assert.That(result.Status, Is.EqualTo(HealthStatus.Healthy));
    }

    [Test]
    public async Task CheckHealthAsync_ReturnsUnhealthy_WhenMinioIsStopped()
    {
        using var client = CreateClient();
        var sut = new MinioHealthCheck(client);

        await _minio.StopAsync();

        var result = await sut.CheckHealthAsync(new HealthCheckContext());

        Assert.That(result.Status, Is.EqualTo(HealthStatus.Unhealthy));
    }

    [Test]
    public async Task CheckHealthAsync_UnhealthyResult_DoesNotLeakExceptionDetails()
    {
        using var client = CreateClient();
        var sut = new MinioHealthCheck(client);

        await _minio.StopAsync();

        var result = await sut.CheckHealthAsync(new HealthCheckContext());

        // No exception object, no description string — an anonymous caller of /health/ready
        // must not learn anything about why or what infrastructure failed.
        Assert.That(result.Exception, Is.Null);
        Assert.That(result.Description, Is.Null.Or.Empty);
    }
}
