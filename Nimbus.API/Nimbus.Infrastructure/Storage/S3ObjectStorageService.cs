using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nimbus.Domain.Enums;
using Nimbus.Domain.Exceptions;
using Nimbus.Domain.Interfaces;
using Nimbus.Domain.ValueObjects;
using Polly;
using Polly.Retry;
namespace Nimbus.Infrastructure.Storage;

/// <summary>
///     The one Infrastructure implementation of <see cref="IObjectStorageService" />, backed by
///     the S3 API (<c>AWSSDK.S3</c>) against a self-hosted, S3-compatible store (MinIO). Nothing
///     here is MinIO-specific: swapping to a hosted S3 provider is a configuration change
///     (<see cref="StorageOptions" />), not a code change.
/// </summary>
public sealed class S3ObjectStorageService : IObjectStorageService
{
    private readonly IAmazonS3 _client;
    private readonly ILogger<S3ObjectStorageService> _logger;
    private readonly StorageOptions _options;
    private readonly ResiliencePipeline _resiliencePipeline;

    public S3ObjectStorageService(IAmazonS3 client, IOptions<StorageOptions> options, ILogger<S3ObjectStorageService> logger)
    {
        _client = client;
        _logger = logger;
        _options = options.Value;
        _resiliencePipeline = BuildResiliencePipeline(_options, logger);
    }

    public async Task UploadAsync(StorageBucket bucket, ObjectKey key, ObjectUpload upload, CancellationToken cancellationToken = default)
    {
        var bucketName = ResolveBucketName(bucket);

        Task PutOnceAsync(CancellationToken ct)
        {
            if (upload.Content.CanSeek)
            {
                upload.Content.Position = 0;
            }

            var request = new PutObjectRequest
            {
                BucketName = bucketName,
                Key = key.Value,
                InputStream = upload.Content,
                ContentType = upload.ContentType,
                AutoCloseStream = false,
                AutoResetStreamPosition = false,
                Headers =
                {
                    ContentLength = upload.ContentLength
                }
            };

            return _client.PutObjectAsync(request, ct);
        }

        try
        {
            // A non-seekable stream cannot be retried safely (the stream may be partially consumed).
            if (!upload.Content.CanSeek && _options.MaxRetryAttempts > 0)
            {
                await PutOnceAsync(cancellationToken);
                return;
            }

            await _resiliencePipeline.ExecuteAsync(PutOnceAsync, cancellationToken);
        }
        catch (Exception ex) when (ex is not ObjectStorageException)
        {
            throw ToObjectStorageException("upload", bucket, key, ex);
        }
    }

    public async Task<ObjectDownload?> DownloadAsync(StorageBucket bucket, ObjectKey key, CancellationToken cancellationToken = default)
    {
        var bucketName = ResolveBucketName(bucket);

        try
        {
            var response = await _resiliencePipeline.ExecuteAsync(
                async ct =>
                {
                    var request = new GetObjectRequest { BucketName = bucketName, Key = key.Value };
                    return await _client.GetObjectAsync(request, ct);
                },
                cancellationToken);

            return new ObjectDownload(response.ResponseStream, response.Headers.ContentType, response.Headers.ContentLength);
        }
        catch (AmazonS3Exception ex) when (IsNotFound(ex))
        {
            return null;
        }
        catch (Exception ex) when (ex is not ObjectStorageException)
        {
            throw ToObjectStorageException("download", bucket, key, ex);
        }
    }

    public async Task DeleteAsync(StorageBucket bucket, ObjectKey key, CancellationToken cancellationToken = default)
    {
        var request = new DeleteObjectRequest { BucketName = ResolveBucketName(bucket), Key = key.Value };

        // DeleteObject on S3/MinIO is already idempotent (a delete of a missing key succeeds), so no
        // special-casing of "not found" is needed here.
        await ExecuteAsync(
            "delete",
            bucket,
            key,
            ct => _client.DeleteObjectAsync(request, ct),
            cancellationToken);
    }

    public async Task<bool> ExistsAsync(StorageBucket bucket, ObjectKey key, CancellationToken cancellationToken = default)
    {
        var bucketName = ResolveBucketName(bucket);

        try
        {
            await _resiliencePipeline.ExecuteAsync(
                async ct =>
                {
                    var request = new GetObjectMetadataRequest { BucketName = bucketName, Key = key.Value };
                    return await _client.GetObjectMetadataAsync(request, ct);
                },
                cancellationToken);

            return true;
        }
        catch (AmazonS3Exception ex) when (IsNotFound(ex))
        {
            return false;
        }
        catch (Exception ex) when (ex is not ObjectStorageException)
        {
            throw ToObjectStorageException("check existence of", bucket, key, ex);
        }
    }

    public async Task<Uri> GetPresignedDownloadUrlAsync(StorageBucket bucket, ObjectKey key)
    {
        var bucketName = ResolveBucketName(bucket);

        try
        {
            var request = new GetPreSignedUrlRequest
            {
                BucketName = bucketName,
                Key = key.Value,
                Verb = HttpVerb.GET,
                Protocol = _options.UseHttps ? Protocol.HTTPS : Protocol.HTTP,
                Expires = DateTime.UtcNow.Add(_options.PresignedUrlExpiry)
            };

            // GetPreSignedURL is a local signature computation (no network call), so it is not
            // wrapped in the retry pipeline — there is nothing transient to retry.
            var url = await _client.GetPreSignedURLAsync(request);
            return new Uri(url);
        }
        catch (Exception ex) when (ex is not ObjectStorageException)
        {
            throw ToObjectStorageException("presign a download URL for", bucket, key, ex);
        }
    }

    private string ResolveBucketName(StorageBucket bucket)
    {
        return bucket switch
        {
            StorageBucket.FlightImages => _options.Buckets.FlightImages,
            StorageBucket.FlightTracks => _options.Buckets.FlightTracks,
            StorageBucket.FlightExports => _options.Buckets.FlightExports,
            StorageBucket.MapCache => _options.Buckets.MapCache,
            _ => throw new ArgumentOutOfRangeException(nameof(bucket), bucket, "Unknown storage bucket.")
        };
    }

    private async Task ExecuteAsync(
        string operation,
        StorageBucket bucket,
        ObjectKey key,
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken)
    {
        try
        {
            await _resiliencePipeline.ExecuteAsync(async ct => await action(ct), cancellationToken);
        }
        catch (Exception ex) when (ex is not ObjectStorageException)
        {
            throw ToObjectStorageException(operation, bucket, key, ex);
        }
    }

    private ObjectStorageException ToObjectStorageException(string operation, StorageBucket bucket, ObjectKey key, Exception inner)
    {
        _logger.LogError(inner, "Object storage operation '{Operation}' failed for {Bucket}/{Key}", operation, bucket, key);

        return new ObjectStorageException(
            $"Unable to {operation} object '{key}' in bucket '{bucket}': the object store is unavailable or the request failed.",
            inner);
    }

    private static bool IsNotFound(AmazonS3Exception ex)
    {
        // Only treat missing object keys as "not found"; missing buckets are configuration errors.
        return string.Equals(ex.ErrorCode, "NoSuchKey", StringComparison.OrdinalIgnoreCase)
               || string.Equals(ex.ErrorCode, "NotFound", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Retries transient failures (network errors, throttling, 5xx responses) with exponential
    ///     backoff, so a momentary storage blip does not surface as an unhandled exception. Not-found
    ///     and other 4xx client errors are not retried — they are handled explicitly by each caller.
    /// </summary>
    private static ResiliencePipeline BuildResiliencePipeline(StorageOptions options, ILogger logger)
    {
        return new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = options.MaxRetryAttempts,
                BackoffType = DelayBackoffType.Exponential,
                Delay = TimeSpan.FromMilliseconds(200),
                UseJitter = true,
                ShouldHandle = new PredicateBuilder().Handle<Exception>(IsTransient),
                OnRetry = args =>
                {
                    logger.LogWarning(
                        args.Outcome.Exception,
                        "Retrying object storage operation (attempt {Attempt}) after transient failure",
                        args.AttemptNumber + 1);
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }

    private static bool IsTransient(Exception ex)
    {
        return ex switch
        {
            AmazonS3Exception s3Ex => (int)s3Ex.StatusCode >= 500 || s3Ex.StatusCode == System.Net.HttpStatusCode.RequestTimeout,
            AmazonServiceException => true,
            TaskCanceledException => false,
            OperationCanceledException => false,
            _ => true
        };
    }
}
