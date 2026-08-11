# MinIO object store — operations notes (issue #95)

This is the operational reference for the `minio` + `minio-init` services in
`infra/docker-compose.prod.yml`. See `infra/RESOURCE-BUDGET.md` for the memory ceiling and disk
budget (already covers MinIO — issue #103) and `infra/DEPLOY-103.md` for the general deploy flow.

## Topology

- `minio` runs with **no `ports:` key** — reachable only on the internal `nimbus` compose network.
  Neither the S3 API (9000) nor the console (9001) is published to the host.
- The console is reachable from outside only through Caddy's `console.$NIMBUS_DOMAIN` route
  (`infra/Caddyfile`), which adds HTTP Basic Auth (`MINIO_CONSOLE_USER` /
  `MINIO_CONSOLE_PASSWORD_HASH`) **in front of** MinIO's own root-credential login — two factors
  guarding the only admin surface for the object store, not one.
- `minio-init` (image `minio/mc`) runs once per `docker compose up`, waits for `minio`'s healthcheck,
  and bootstraps buckets/policy/app-user. It is safe to re-run — see `infra/minio-init.sh`.

## Credentials

Two, deliberately different, credential pairs — generated at deploy time, never defaulted:

| Env var | Used by | Scope |
|---|---|---|
| `MINIO_ROOT_USER` / `MINIO_ROOT_PASSWORD` | `minio-init` only, and human console login | Full admin — never given to the application |
| `MINIO_APP_ACCESS_KEY` / `MINIO_APP_SECRET_KEY` | The API | Read/write/list/delete on exactly `flight-images`, `flight-tracks`, `flight-exports`, `map-cache` — no admin actions |

Generate all four with `openssl rand -base64 24` (or similar) alongside the other secrets in
`nimbus-issue-5-STEPS.md` Part D2. `MINIO_CONSOLE_PASSWORD_HASH` is generated separately:

```bash
docker run --rm caddy:2-alpine caddy hash-password --plaintext '<console password>'
```

## Buckets, policy, and versioning

`minio-init.sh` creates the four buckets from issue #11's convention
(`flight-images`, `flight-tracks`, `flight-exports`, `map-cache`), attaches a policy scoped to only
those buckets to the dedicated app user, and explicitly sets `mc anonymous set none` on each —
idempotently, so a rebuild needs no console clicking.

### Versioning outcome (issue #11's open question)

**Confirmed working** on this single-node topology. Tested directly against
`minio/minio:latest` (RELEASE.2025-09-07): `mc version enable` on a single-drive, single-node
instance succeeds and `mc version info` reports versioning enabled. `minio-init.sh` therefore enables
versioning on all four buckets unconditionally. This means #78's accidental-deletion recoverability
*can* rely on bucket versioning here — it does not have to come from the backup job alone (the backup
job remains necessary regardless, per #95's own note that this is the only copy of pilot media on the
host).

### Anonymous access — verified, not assumed

Verified directly (not inferred from MinIO's default private ACL) with a real container and `curl`:

```bash
# From a container on the same compose network as minio, with no credentials:
curl -s -o /dev/null -w "%{http_code}\n" http://minio:9000/flight-images/
# → 403 (AccessDenied)
curl -s -o /dev/null -w "%{http_code}\n" http://minio:9000/flight-images/anything.jpg
# → 403 (AccessDenied)
```

Both return `403 AccessDenied`, not `200`/listing or object content. Re-run this check after any
change to `minio-init.sh`'s `mc anonymous set` calls or to bucket policy.

### App key least-privilege — verified

```bash
mc alias set appuser http://minio:9000 "$MINIO_APP_ACCESS_KEY" "$MINIO_APP_SECRET_KEY"
mc cp somefile.txt appuser/flight-images/somefile.txt   # succeeds — in scope
mc mb appuser/some-other-bucket                          # → Access Denied — cannot create buckets
mc admin info appuser                                    # → Access Denied — no admin rights
```

## RAM and disk budget

Already covered by issue #103 — see `infra/RESOURCE-BUDGET.md`:
- Memory: `mem_limit: 1g`, `mem_reservation: 256m`, `cpus: 1.5` in `infra/docker-compose.limits.yml`.
- Disk: `/srv/nimbus/data/minio` is budgeted at ~100 GB and alerted on independently of the root
  filesystem via the `NimbusVolumeBudgetExceeded` rule in `infra/alert.rules.yml` (fires above 80 GB).

## Restoring from backup (verify once, then keep as a runbook)

This must be performed once against the deployed VPS before #95 can be closed — it cannot be done
from this environment. Procedure:

1. **Stop the stack** (or at minimum `minio`) so nothing writes to the volume mid-restore:
   ```bash
   docker compose --profile app stop minio
   ```
2. **Move the current data volume aside** rather than deleting it, in case the restore needs a diff:
   ```bash
   sudo mv /srv/nimbus/data/minio /srv/nimbus/data/minio.bak-$(date +%Y%m%d)
   sudo install -d -m 750 -o 1000 -g 1000 /srv/nimbus/data/minio
   ```
3. **Restore via restic** into the now-empty directory (adjust snapshot ID/path to the actual restic
   setup once #<backup-issue> lands):
   ```bash
   restic -r "$RESTIC_REPOSITORY" restore latest --target / --include /srv/nimbus/data/minio
   sudo chown -R 1000:1000 /srv/nimbus/data/minio
   ```
4. **Bring MinIO back up** and re-run `minio-init` (idempotent — recreates the policy/app-user if the
   restored data predates them, and does not disturb restored objects):
   ```bash
   docker compose --profile app up -d minio
   docker compose --profile app up minio-init
   ```
5. **Verify objects are readable through the application**, not just through `mc`: exercise an actual
   API endpoint that serves a previously-uploaded flight image/export and confirm the bytes match
   what was backed up. Record the date this was performed and the result here:

   > _Restore-from-backup test performed: **not yet run** — pending live VPS + backup job (tracked
   > separately). Update this line with the date and outcome once done._
