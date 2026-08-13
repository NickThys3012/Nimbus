using Nimbus.Domain.Enums;
using Nimbus.Domain.ValueObjects;
namespace Nimbus.Domain.Interfaces;

/// <summary>
///     Content of an object being uploaded to the store. Content type and length are required so
///     the Infrastructure implementation always sets them on upload (browsers then render e.g. an
///     image inline instead of downloading it).
/// </summary>
/// <param name="Content">The object's byte stream. The caller owns disposal.</param>
/// <param name="ContentType">The MIME type, e.g. <c>image/jpeg</c>.</param>
/// <param name="ContentLength">The exact length, in bytes, of <paramref name="Content" />.</param>
public sealed record ObjectUpload(Stream Content, string ContentType, long ContentLength);

/// <summary>
///     A downloaded object: its content stream plus the metadata the store recorded on upload.
/// </summary>
/// <param name="Content">The object's byte stream. The caller is responsible for disposing it.</param>
/// <param name="ContentType">The MIME type recorded at upload time.</param>
/// <param name="ContentLength">The object's length, in bytes.</param>
public sealed record ObjectDownload(Stream Content, string ContentType, long ContentLength) : IAsyncDisposable
{
    public async ValueTask DisposeAsync()
    {
        await Content.DisposeAsync();
    }
}

/// <summary>
///     Domain abstraction over an S3-compatible object store. One Infrastructure implementation
///     backs this (currently over the S3 API against a self-hosted MinIO instance), so every
///     feature that handles binary content (flight images, tracks, exports, map tiles) goes
///     through the same contract and the backing store can be swapped — including for a hosted S3
///     provider later — without touching feature code.
/// </summary>
public interface IObjectStorageService
{
    /// <summary>
    ///     Uploads an object to <paramref name="bucket" /> under <paramref name="key" />, setting
    ///     content type and content length on the stored object. Transient failures are retried
    ///     with backoff by the implementation; on exhaustion this throws
    ///     <see cref="Exceptions.ObjectStorageException" /> rather than letting the underlying SDK
    ///     exception escape.
    /// </summary>
    Task UploadAsync(StorageBucket bucket, ObjectKey key, ObjectUpload upload, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Downloads the object at <paramref name="key" /> from <paramref name="bucket" />.
    ///     Returns <c>null</c> if no such object exists.
    /// </summary>
    Task<ObjectDownload?> DownloadAsync(StorageBucket bucket, ObjectKey key, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Deletes the object at <paramref name="key" /> from <paramref name="bucket" />. Deleting
    ///     a key that does not exist is not an error (idempotent).
    /// </summary>
    Task DeleteAsync(StorageBucket bucket, ObjectKey key, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Returns whether an object exists at <paramref name="key" /> in <paramref name="bucket" />.
    /// </summary>
    Task<bool> ExistsAsync(StorageBucket bucket, ObjectKey key, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Issues a short-lived, presigned download URL for the object at <paramref name="key" />
    ///     in <paramref name="bucket" />. Buckets are private, so this — or serving the bytes
    ///     through the API — is the only way to hand a client access to an object; there is no
    ///     permanent public URL. The lifetime is controlled entirely by configuration
    ///     (<c>Storage:PresignedUrlExpiry</c>), never a literal in calling code.
    /// </summary>
    Task<Uri> GetPresignedDownloadUrlAsync(StorageBucket bucket, ObjectKey key);
}
