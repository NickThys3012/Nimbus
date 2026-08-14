# Nimbus
Propper implementation of flightPrep (POC)

## Local development (issue #3)

**Prerequisites**: Docker Desktop (or Docker Engine + Compose v2) — `docker compose version` must
report v2.x. Nothing else needs to be installed locally; the Angular app and .NET API are both
built inside the containers.

```bash
cp .env.example .env
docker compose up --build
```

This starts the API (with the Angular frontend built in and served from the same origin), SQL
Server, MinIO and Mailpit, and applies EF Core migrations automatically via the one-shot
`migrator` container.

| Service | URL | Notes |
|---|---|---|
| App (API + Angular) | http://localhost:8080 | Same origin — no separate frontend port |
| MinIO console | http://localhost:9001 | Login with `MINIO_ROOT_USER`/`MINIO_ROOT_PASSWORD` from `.env` |
| Mailpit | http://localhost:8025 | Captures all outbound email — nothing is ever sent from a dev machine |
| SQL Server | *(not published)* | Uncomment the commented-out `ports:` entry under `sqlserver` in `docker-compose.yml` to reach `localhost:1433` (e.g. from SSMS/Azure Data Studio) — left off by default so a locally installed SQL Server isn't clashed with |

Database and object-store data persist across restarts in named Docker volumes. To reset both to a
clean slate:

```bash
docker compose down -v
```

**Apple Silicon (M-series) note**: `mcr.microsoft.com/mssql/server` has no arm64 build (verified —
its published manifest is single-platform `amd64`, not a multi-arch list), so `sqlserver` runs
under emulation. It works, just with a noticeably slower cold start — the healthcheck's
`start_period` already accounts for this. `migrator`'s self-contained bundle is also hardcoded to
`linux-x64` (see `Dockerfile`), so it's pinned to `platform: linux/amd64` in `docker-compose.yml` —
without that pin, an arm64 host builds an arm64 image around an x64-only executable, which fails
at startup rather than just running slowly. MinIO and Mailpit both have native arm64 images, and
the `api` image is framework-dependent (portable IL), so it builds and runs natively on arm64.

See [`docs/configuration.md`](docs/configuration.md) for the equivalent production setup and
[`infra/MINIO.md`](infra/MINIO.md) for MinIO operational details.
