# Deploying issue #2 (SQL Server persistence with EF Core and migrations) to the VPS

This is the delta on top of `infra/nimbus-issue-5-STEPS.md` Part D (which stood the `sqlserver`
container up with `MSSQL_SA_PASSWORD`). Issue #2 introduces one new container
(`sqlserver-init`), two new secrets (`MSSQL_APP_PASSWORD`/`MSSQL_MIGRATOR_PASSWORD`), two new
files (`sqlserver-init.sh`/`.sql`), and changes what the `api`/`migrator` services actually
authenticate against the database as. No CI/CD pipeline builds and pushes the `nimbus-api` /
`nimbus-migrator` images yet (that's issue #6) — until it exists, build and push them manually
(step 2).

## 1. Copy the changed/new files `[LOCAL]`

```bash
cd ~/path/to/Nimbus
scp infra/docker-compose.prod.yml deploy@<vps-ip>:/opt/nimbus/compose.yaml
scp infra/sqlserver-init.sh infra/sqlserver-init.sql deploy@<vps-ip>:/opt/nimbus/
```

As with every prior deploy, `docker-compose.prod.yml` **must land as `/opt/nimbus/compose.yaml`**
— that is the filename Compose reads on the VPS. `sqlserver-init.sh`/`.sql` land next to it
un-renamed; the `sqlserver-init` service mounts them by that exact name.

## 2. Build and push the `api`/`migrator` images `[LOCAL]`

No image has been published for either service yet. Until issue #6's CD pipeline exists, build
and push both targets from the repo-root `Dockerfile` manually:

```bash
cd ~/path/to/Nimbus
docker build --target api      -t ghcr.io/nickthys3012/nimbus-api:latest .
docker build --target migrator -t ghcr.io/nickthys3012/nimbus-migrator:latest .
docker login ghcr.io   # if not already logged in, needs write:packages
docker push ghcr.io/nickthys3012/nimbus-api:latest
docker push ghcr.io/nickthys3012/nimbus-migrator:latest
```

## 3. `.env` — add two new secrets `[VPS]`

```bash
cd /opt/nimbus
openssl rand -base64 24   # run twice, once per line below
```

Add both to `/opt/nimbus/.env` (see `infra/.env.example` for placement) and record them in the
password manager per `docs/configuration.md` §5:

```
MSSQL_APP_PASSWORD=<generated>
MSSQL_MIGRATOR_PASSWORD=<generated>
```

`MSSQL_SA_PASSWORD` already exists from issue #5 — nothing to change there; it's now used only for
the `sqlserver` healthcheck and by `sqlserver-init`'s one-time login bootstrap, never by the API.

## 4. What changed in `compose.yaml`

- **New service `sqlserver-init`**: one-shot, idempotent (same pattern as `minio-init`). Waits for
  `sqlserver` to report healthy, then runs `sqlserver-init.sql` via `sqlcmd` as `sa` to create the
  `Nimbus` database and two least-privilege SQL logins:
  - `nimbus_app` (`db_datareader`/`db_datawriter` only) — what `api` connects as.
  - `nimbus_migrator` (`db_owner`, i.e. DDL rights) — what only the transient `migrator` container
    connects as.
- **`sqlserver`**: now has a `healthcheck` (`sqlcmd -Q "SELECT 1"`, 30s start period — SQL Server's
  cold start is slow).
- **`api`**: `depends_on` now also requires `sqlserver: condition: service_healthy` (in addition to
  the existing `migrator: condition: service_completed_successfully`); its connection string now
  authenticates as `nimbus_app`, not `sa`.
- **`migrator`**: `depends_on` now requires `sqlserver: condition: service_healthy` **and**
  `sqlserver-init: condition: service_completed_successfully` (so the login exists before it tries
  to connect); its connection string authenticates as `nimbus_migrator`, not `sa`.

## 5. Verify the merged config `[VPS]`

```bash
cd /opt/nimbus
docker compose --profile app config > /dev/null && echo "CONFIG OK"
docker compose --profile app config | grep -A3 '^  api:' | grep -i 'connectionstrings'
docker compose --profile app config | grep -A3 '^  migrator:' | grep -i 'connectionstrings'
```

Confirm both `ConnectionStrings__Database` values resolve to real (non-empty) passwords, not
literal `${MSSQL_APP_PASSWORD}`/`${MSSQL_MIGRATOR_PASSWORD}` text — an unresolved reference means
`.env` is missing the variable (see step 3).

## 6. Roll out `[VPS]`

```bash
cd /opt/nimbus
docker compose --profile app up -d sqlserver
docker compose --profile app up -d sqlserver-init
docker compose --profile app logs sqlserver-init --tail 50   # confirm "SQL Server bootstrap complete"
docker compose --profile app up -d migrator
docker compose --profile app logs migrator --tail 50         # confirm "Applying migration ... Done."
docker compose --profile app up -d api
docker compose --profile app logs api --tail 50
```

If `migrator` exits non-zero, `api` will not start (`condition: service_completed_successfully`)
and the previous `api` container keeps running — check `migrator`'s logs before re-running it.

## 7. Smoke-test the least-privilege logins `[VPS]`

Confirm the API genuinely cannot do DDL, proving it isn't silently still connecting as `sa`:

```bash
docker exec -it $(docker compose --profile app ps -q sqlserver) \
  /opt/mssql-tools18/bin/sqlcmd -S localhost -U nimbus_app -P "$MSSQL_APP_PASSWORD" -C \
  -d Nimbus -Q "CREATE TABLE ShouldFail (Id INT)"
```

This must fail with a permissions error (`nimbus_app` has no `db_ddladmin`/`db_owner` role). A
`SELECT` against `AspNetUsers` with the same login should succeed.

## What issue #2 does *not* change here

- **No change to `/srv/nimbus/data/mssql` ownership.** Still `10001:0`, set during issue #5
  (`infra/nimbus-issue-5-STEPS.md` Part C1) — the `sqlserver` service still runs as the image's
  built-in non-root `mssql` user with no explicit `user:` override.
- **No change to `MSSQL_PID`/licensing.** Still `Express` by default; see `docs/configuration.md`
  §9 for the recorded 10 GB/database, ~1.4 GB buffer-pool, 1-socket/4-core limits and when to
  revisit them.
- **No CD pipeline yet.** Building/pushing `nimbus-api`/`nimbus-migrator` (step 2) stays a manual
  step until issue #6 lands.
