# Deploying issue #11 (object storage abstraction) to the VPS

This is the delta on top of `infra/nimbus-issue-5-STEPS.md` Part D and `DEPLOY-95.md` (which stood
the `minio` + `minio-init` containers up) and `DEPLOY-12.md`/`DEPLOY-103.md` (observability, resource
limits). Issue #11 introduces no new container and no new secret — MinIO and its dedicated app
credential (`MINIO_APP_ACCESS_KEY`/`MINIO_APP_SECRET_KEY`) already exist per `DEPLOY-95.md`. What
changes here is that the `api` service now actually authenticates against MinIO through
`IObjectStorageService`/`S3ObjectStorageService`, so it needs those credentials handed to it as
`Storage:*` config, and it must not start before `minio-init` has provisioned the buckets/policy.

## 1. Copy the changed file `[LOCAL]`

```bash
cd ~/path/to/Nimbus
scp infra/docker-compose.prod.yml deploy@<vps-ip>:/opt/nimbus/compose.yaml
```

As with every prior deploy, this **must land as `/opt/nimbus/compose.yaml`** (not
`docker-compose.prod.yml`) — that is the filename Compose reads on the VPS. Copying it under its
source name is a no-op and `docker compose up -d` will report `api` as already "Running" instead of
recreating it with the new environment block.

## 2. `.env` — nothing to add `[VPS]`

No new environment variables are required. The `api` service's new `Storage__AccessKey` /
`Storage__SecretKey` values are sourced from the **existing** `MINIO_APP_ACCESS_KEY` /
`MINIO_APP_SECRET_KEY` pair already in `/opt/nimbus/.env` (see `DEPLOY-95.md` / `infra/MINIO.md`).
Confirm they're still present and non-empty before rolling out — no need to generate or rotate
anything for this deploy:

```bash
grep -E '^MINIO_APP_ACCESS_KEY=|^MINIO_APP_SECRET_KEY=' /opt/nimbus/.env
```

## 3. What changed in `compose.yaml`'s `api` service

```yaml
environment:
  Storage__Endpoint: http://minio:9000
  Storage__AccessKey: ${MINIO_APP_ACCESS_KEY}
  Storage__SecretKey: ${MINIO_APP_SECRET_KEY}
  Storage__ForcePathStyle: "true"
  Storage__UseHttps: "false"
depends_on:
  migrator:
    condition: service_completed_successfully
  minio-init:
    condition: service_completed_successfully
```

- `Storage__*` uses ASP.NET Core's double-underscore convention (`Storage__AccessKey` →
  configuration key `Storage:AccessKey`), same pattern as `Loki__Url` in `DEPLOY-12.md`.
- `Storage__Endpoint` is the in-network MinIO address (`minio:9000`, no TLS — internal traffic only,
  never exposed past the `nimbus` bridge network), so `ForcePathStyle`/`UseHttps` are fixed at
  `"true"`/`"false"` rather than sourced from `.env`.
- The new `depends_on: minio-init` ensures the API never starts against buckets that don't exist yet
  — `minio-init` creates `flight-images`, `flight-tracks`, `flight-exports`, `map-cache` idempotently
  on every `docker compose up` (see `infra/minio-init.sh`).

## 4. Verify the merged config `[VPS]`

```bash
cd /opt/nimbus
docker compose --profile app config > /dev/null && echo "CONFIG OK"
docker compose --profile app config | grep -A5 '^  api:' | grep -i 'storage__'
```

Confirm `Storage__AccessKey`/`Storage__SecretKey` resolve to real (non-empty) values, not literal
`${MINIO_APP_ACCESS_KEY}` text — an unresolved reference means `.env` is missing the variable (see
step 2).

## 5. Roll out `[VPS]`

```bash
cd /opt/nimbus
docker compose --profile app up -d minio minio-init
docker compose --profile app up -d migrator api
docker compose --profile app logs api --tail 50   # confirm no ObjectStorageException / options-validation error at startup
```

`StorageOptions` is validated on start (`ValidateOnStart`) — if `Storage:Endpoint`,
`Storage:AccessKey` or `Storage:SecretKey` resolve empty, the API fails fast at boot with a clear
options-validation error rather than a first-request failure. That's the surface to check first if
`api` doesn't come up healthy after this deploy.

## 6. Smoke-test object storage end-to-end `[VPS]`

Exercise a real upload/download through the running API (adjust to whatever endpoint currently
accepts a flight image/export upload), then confirm the object landed under the documented key
convention:

```bash
docker exec -it $(docker compose --profile app ps -q minio) sh -c \
  "mc alias set local http://localhost:9000 \$MINIO_ROOT_USER \$MINIO_ROOT_PASSWORD && \
   mc ls local/flight-images --recursive"
```

Each listed key should look like `{ownerId}/{flightId}/{fileName}` (or `map-cache`'s own
non-owner-scoped path) — see `docs/object-storage.md` for the full convention.

## What issue #11 does *not* change here

- **No new container.** `minio`/`minio-init` already exist from `DEPLOY-95.md`; this deploy only
  changes how `api` authenticates against them.
- **No bucket/policy changes.** The four buckets, their versioning and their private-access policy
  are unchanged — still provisioned entirely by `infra/minio-init.sh`.
- **No new secrets to generate or rotate.** Reuses `MINIO_APP_ACCESS_KEY`/`MINIO_APP_SECRET_KEY`
  as-is.
- **Restore-from-backup verification** (the open item at the bottom of `infra/MINIO.md`) is still
  pending and unrelated to this deploy — it's a backup-job exercise, not something this change
  affects.
