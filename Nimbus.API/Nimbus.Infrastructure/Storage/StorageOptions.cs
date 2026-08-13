namespace Nimbus.Infrastructure.Storage;

/// <summary>
///     Configuration for the S3-compatible object store (bound from the <c>Storage</c> config
///     section — see appsettings.json / environment variables). Endpoint, credentials, region and
///     path-style addressing are all configuration so the identical
///     <see cref="S3ObjectStorageService" /> runs unchanged in development (self-hosted MinIO) and
///     production, and could point at a hosted S3 provider later without a code change.
/// </summary>
public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>The S3-compatible endpoint, e.g. <c>http://minio:9000</c>.</summary>
    public string Endpoint { get; init; } = string.Empty;

    /// <summary>Access key for the dedicated, least-privilege application user (never root).</summary>
    public string AccessKey { get; init; } = string.Empty;

    /// <summary>Secret key for the dedicated, least-privilege application user (never root).</summary>
    public string SecretKey { get; init; } = string.Empty;

    /// <summary>Region sent on requests. MinIO ignores it but the SDK requires a value.</summary>
    public string Region { get; init; } = "us-east-1";

    /// <summary>
    ///     Path-style addressing (<c>endpoint/bucket/key</c>) instead of virtual-hosted style
    ///     (<c>bucket.endpoint/key</c>) — required for a self-hosted store without per-bucket DNS.
    /// </summary>
    public bool ForcePathStyle { get; init; } = true;

    /// <summary>Whether the endpoint is served over HTTPS.</summary>
    public bool UseHttps { get; init; } = true;

    /// <summary>
    ///     Lifetime of presigned download URLs. Kept short and always sourced from configuration —
    ///     never a literal in calling code.
    /// </summary>
    public TimeSpan PresignedUrlExpiry { get; init; } = TimeSpan.FromMinutes(15);

    /// <summary>Number of retry attempts, with backoff, for transient failures.</summary>
    public int MaxRetryAttempts { get; init; } = 3;

    /// <summary>The real bucket name backing each <see cref="Nimbus.Domain.Enums.StorageBucket" />.</summary>
    public StorageBucketNames Buckets { get; init; } = new();
}

/// <summary>
///     Maps each logical <see cref="Nimbus.Domain.Enums.StorageBucket" /> to the real bucket name
///     configured for this environment. Buckets are configured, not hard-coded, so an environment
///     can rename/namespace them (e.g. for a shared dev cluster) without a code change.
/// </summary>
public sealed class StorageBucketNames
{
    public string FlightImages { get; init; } = "flight-images";
    public string FlightTracks { get; init; } = "flight-tracks";
    public string FlightExports { get; init; } = "flight-exports";
    public string MapCache { get; init; } = "map-cache";
}
