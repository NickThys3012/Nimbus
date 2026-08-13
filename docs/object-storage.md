# Object storage (issue #11)

Every feature that handles binary content (flight images, flight tracks, generated exports, map
tiles) goes through one abstraction, so the backing store can change without touching feature code.

## Layers

- **`Nimbus.Domain.Interfaces.IObjectStorageService`** — the Domain contract: `UploadAsync`,
  `DownloadAsync`, `DeleteAsync`, `ExistsAsync`, `GetPresignedDownloadUrlAsync`. Application code
  depends only on this interface, never on an S3/MinIO type.
- **`Nimbus.Infrastructure.Storage.S3ObjectStorageService`** — the one implementation, over the S3
  API (`AWSSDK.S3`), registered in `Nimbus.Infrastructure.DependencyInjection`. It runs unmodified
  against self-hosted MinIO today and could point at a hosted S3 provider later purely by changing
  configuration.
- **`Nimbus.Domain.Enums.StorageBucket`** — the logical buckets the application knows about:
  `FlightImages`, `FlightTracks`, `FlightExports`, `MapCache`. Callers never reference a raw bucket
  name; `StorageOptions.Buckets` maps each enum value to the real, configured bucket name (see
  `infra/minio-init.sh` for how those four buckets are provisioned).

## Configuration

Bound from the `Storage` config section (`appsettings.json`, environment variables, or — in
production — `Storage__*` variables set on the `api` service in `infra/docker-compose.prod.yml`,
sourced from the `MINIO_APP_ACCESS_KEY`/`MINIO_APP_SECRET_KEY` pair in `.env`; see
`infra/MINIO.md`):

| Key | Purpose |
|---|---|
| `Storage:Endpoint` | S3-compatible endpoint, e.g. `http://minio:9000` |
| `Storage:AccessKey` / `Storage:SecretKey` | Credentials for the dedicated, least-privilege application user — never MinIO root |
| `Storage:Region` | Sent on requests; MinIO ignores it, the SDK requires a value |
| `Storage:ForcePathStyle` | `true` for a self-hosted store without per-bucket DNS |
| `Storage:UseHttps` | Whether the endpoint is served over HTTPS |
| `Storage:PresignedUrlExpiry` | Lifetime of presigned download URLs (`TimeSpan`, e.g. `00:15:00`) — always sourced from config, never a literal in code |
| `Storage:MaxRetryAttempts` | Retry attempts, with exponential backoff, for transient failures |
| `Storage:Buckets:*` | The real bucket name behind each `StorageBucket` value |

## Object-key convention

Every object in an owner/flight-scoped bucket (`flight-images`, `flight-tracks`, `flight-exports`)
uses the key shape built by `Nimbus.Domain.ValueObjects.ObjectKey.ForFlightAsset`:

```
{ownerId}/{flightId}/{fileName}
```

This means an object's owner and originating flight can always be recovered from its key alone,
without a database lookup — so an orphaned object (e.g. left behind after a flight delete that
failed partway) is identifiable by listing a bucket and checking each `{ownerId}/{flightId}` prefix
against the database, with no separate index to maintain.

`map-cache` is not owner/flight scoped — it holds shared, content-addressable tiles reused across
flights — so it uses `ObjectKey.ForSharedAsset` with a caller-supplied relative path (e.g. a
`{z}/{x}/{y}` tile path) instead.

## Access model

- Buckets are private (`mc anonymous set none`, verified in `infra/MINIO.md`). There is no
  permanent public URL for any object.
- Clients are handed access either by the API streaming bytes itself, or via
  `GetPresignedDownloadUrlAsync`, which returns a time-limited URL whose lifetime is
  `Storage:PresignedUrlExpiry` — short, and always configuration-driven.
- Uploads always set content type and content length (`ObjectUpload.ContentType` /
  `ContentLength`), so a browser renders e.g. an image inline instead of downloading it.

## Failure handling

Transient failures (network errors, request timeouts, 5xx/throttling responses) are retried with
exponential backoff (`Storage:MaxRetryAttempts`, via Polly) inside `S3ObjectStorageService`. Once
retries are exhausted — or for a non-retryable failure — the SDK exception is translated into
`Nimbus.Domain.Exceptions.ObjectStorageException`, so a storage outage surfaces to calling code as a
handled, typed error rather than an unhandled `AmazonS3Exception`.

## Testing

Integration tests in `Nimbus.Infrastructure.Tests` run `S3ObjectStorageService` against a real MinIO
container via Testcontainers (`Testcontainers.Minio`) — not a mock — covering upload/download,
content-type/length round-tripping, delete idempotency, existence checks, and presigned URL
generation.
