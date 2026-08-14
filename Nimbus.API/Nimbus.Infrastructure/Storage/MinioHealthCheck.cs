using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Nimbus.Infrastructure.Storage;

/// <summary>
///     Readiness check for the S3-compatible object store (MinIO in development/production —
///     issue #97). <see cref="IAmazonS3.ListBucketsAsync(CancellationToken)" /> is the cheapest
///     call the SDK offers that still requires MinIO to actually authenticate and respond, so
///     this is safe to poll every few seconds without measurable load. It deliberately reports
///     only healthy/unhealthy — no endpoint, bucket name or exception detail is surfaced, so a
///     failure can't leak infrastructure topology to an anonymous caller.
/// </summary>
public sealed class MinioHealthCheck(IAmazonS3 s3) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await s3.ListBucketsAsync(new ListBucketsRequest(), cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception)
        {
            // No exception message/stack trace surfaced — see class remarks.
            return HealthCheckResult.Unhealthy();
        }
    }
}
