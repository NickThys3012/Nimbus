# Configuration and secrets inventory (issue #98)

Every configuration value the production stack needs, where it lives, and — for secrets — who owns
it and when it must rotate. If the VPS is lost, this document plus `infra/nimbus-issue-5-STEPS.md`
is all that is needed to rebuild it; nothing should be "remembered".

Three places hold configuration, and each has exactly one job:

| Source | What lives there | Committed? |
|---|---|---|
| GitHub Actions secrets | Values the CD pipeline (issue #6) needs to reach and authenticate against the VPS | No — configured in repo/environment settings |
| `/opt/nimbus/.env` (server) | Every runtime value `docker-compose.prod.yml` interpolates: passwords, keys, domain, feature flags | No — `infra/.env` is gitignored, never leaves the VPS except into a password manager |
| `infra/.env.example` (repo) | The full list of keys above, each with an empty or placeholder value, so a rebuild starts from a complete template | Yes |

## 1. Full settings inventory

| Name | Purpose | Lives in | Sensitive |
|---|---|---|---|
| `NIMBUS_DOMAIN` | Base domain; Caddy derives `nimbus.<domain>`, `grafana.<domain>`, `console.<domain>` | `.env` | No |
| `ACME_EMAIL` | Contact address for Let's Encrypt | `.env` | No |
| `ASPNETCORE_ENVIRONMENT` | .NET hosting environment | `.env` (committed default `Production` in `.env.example`) | No |
| `IMAGE_TAG` | GHCR image tag to deploy | `.env` (compose default `latest` — not a secret, safe to fall back) | No |
| `MSSQL_SA_PASSWORD` | SQL Server `sa` login password | `.env` | **Yes** |
| `MSSQL_PID` | SQL Server edition | `.env` (committed default `Express`) | No |
| `MSSQL_MEMORY_LIMIT_MB` | SQL Server's own buffer-pool ceiling (paired with the container `mem_limit` in `docker-compose.limits.yml`) | `.env` | No |
| `MINIO_ROOT_USER` / `MINIO_ROOT_PASSWORD` | MinIO admin credential — console + `minio-init` only | `.env` | **Yes** |
| `MINIO_APP_ACCESS_KEY` / `MINIO_APP_SECRET_KEY` | Scoped credential the API authenticates with (see `infra/MINIO.md`) | `.env` | **Yes** |
| `MINIO_CONSOLE_USER` / `MINIO_CONSOLE_PASSWORD_HASH` | Caddy basic-auth in front of the MinIO console route | `.env` | **Yes** (hash) |
| `GRAFANA_ADMIN_USER` / `GRAFANA_ADMIN_PASSWORD` | Grafana admin login | `.env` | **Yes** |
| `Loki__Url` | Serilog sink target: the API's own structured logs pushed to Loki (issue #12) | `.env` (committed default `http://loki:3100`, internal-network only, not a secret) | No |
| `RESTIC_REPOSITORY` | Backup destination URL | `.env` | No (destination, not a credential) |
| `RESTIC_PASSWORD` | Backup repository encryption key | `.env` **and** off-server (see §5) | **Yes** |
| `EMAIL_PROVIDER_API_KEY` | Transactional email provider auth | `.env` | **Yes** |
| `EMAIL_FROM` | From-address for outgoing mail | `.env` | No |
| `VPS_HOST` | VPS address the CD pipeline deploys to | GitHub Actions secret | No (not a credential, but kept as a secret to avoid advertising the target) |
| `VPS_SSH_KEY` | Private half of the dedicated deploy keypair | GitHub Actions secret | **Yes** |
| GHCR pull token (`read:packages`) | Lets the VPS `docker login ghcr.io` and pull private images | Stored only in the VPS's Docker credential store (`~/.docker/config.json` for `deploy`), **not** a GitHub Actions secret — the pipeline pushes, the VPS pulls | **Yes** |

`docker-compose.prod.yml` and `docker-compose.limits.yml` reference every sensitive value above via
plain `${VAR}` — **no default fallback exists for any secret** (only the non-secret `IMAGE_TAG`
falls back to `latest`). This is verified before every deploy:

```bash
# infra/.env.example with every value blanked out — the empty-.env check
cd /opt/nimbus
mv .env .env.bak
if docker compose --profile app config | grep -Eq ':\s*$'; then echo "ERROR: one or more required env vars resolved to empty"; exit 1; fi
docker compose --profile app up -d       # should fail fast if any required value is missing/invalid
mv .env.bak .env
```

## 2. GitHub Actions secrets

Exactly these three live in the repo (or environment) settings — no more:

| Secret | Used for | Rotation procedure |
|---|---|---|
| `VPS_HOST` | SSH target for the CD pipeline (issue #6) | Update if the VPS is rebuilt on a new IP/host; no credential material, rotate opportunistically |
| `VPS_SSH_KEY` | Private key for the dedicated `nimbus-deploy` keypair (see §4) | 1. Generate a new pair (`ssh-keygen -t ed25519 -f nimbus-deploy -C "nimbus-ci"`) locally. 2. Append the new public key to `deploy`'s `authorized_keys` on the VPS. 3. Update the `VPS_SSH_KEY` secret with the new private key. 4. Confirm a pipeline run succeeds. 5. Remove the old public key from `authorized_keys`. Rotate at least yearly or immediately on suspected compromise |
| `EMAIL_PROVIDER_API_KEY` | Same value as the server `.env` entry, injected into staging/preview environments the pipeline may spin up | Rotate through the provider's dashboard, update this secret and the server `.env` together (§5 procedure) |

Secrets are referenced only as `${{ secrets.NAME }}` inside workflow `env:`/`with:` blocks, never
echoed to logs, and never used to construct a shell string that gets printed (`set -x` is disabled
in any step touching them).

## 3. Server `.env`

- Path: `/opt/nimbus/.env`
- Owner: `deploy:deploy`
- Mode: `600` (verify with `stat -c '%a %U:%G' /opt/nimbus/.env`)
- Template: `infra/.env.example`, committed with every key present and no real values — copy it to
  the server, fill it in, `chmod 600`. Never edit `.env.example` to hold a real secret.
- `infra/.env` (the filled, local-editing copy) is listed in `.gitignore`; `git check-ignore -v
  infra/.env` must print a match before it is ever created locally.

## 4. Deploy keypair

The CD pipeline authenticates with a keypair generated solely for this purpose (`nimbus-deploy`),
never a maintainer's personal SSH key:

```bash
ssh-keygen -t ed25519 -f nimbus-deploy -C "nimbus-ci-deploy" -N ""
```

The public half goes into `deploy`'s `authorized_keys` on the VPS (`infra/nimbus-issue-5-STEPS.md`
Part C4); the private half becomes the `VPS_SSH_KEY` GitHub secret and lives nowhere else. Because
it is single-purpose, revoking it (delete the line from `authorized_keys`) never affects a
maintainer's own access to the box.

## 5. Secrets inventory: owner and rotation

Every credential below must have a named owner and a recorded last-rotation date. Track the actual
dates in your password manager entry for each item (the table below is the schedule; fill in real
dates as rotations happen — do not let this file itself become the source of truth for *when* a
rotation last happened, only for the *policy*).

| Secret | Owner | Rotation interval | Rotation procedure |
|---|---|---|---|
| SQL Server `sa` password (`MSSQL_SA_PASSWORD`) | Maintainer | 90 days, or on suspected compromise | Generate with `openssl rand -base64 24` (must contain a digit — SQL Server's password policy rejects weak strings), update `.env`, `docker compose up -d sqlserver`, confirm the API reconnects |
| MinIO root credential | Maintainer | 90 days | Generate new pair, update `.env`, `docker compose up -d minio`, confirm `minio-init` still applies policy on next run |
| MinIO app access key (`MINIO_APP_ACCESS_KEY`/`MINIO_APP_SECRET_KEY`) | Maintainer | 90 days | Generate new pair, update `.env`, `docker compose up -d minio-init`, confirm the API's object-storage calls still succeed before removing the old key from MinIO |
| Grafana admin password | Maintainer | 90 days | Update `.env`, `docker compose up -d grafana`, log in to confirm |
| Restic repository password (`RESTIC_PASSWORD`) | Maintainer | On suspected compromise only (rotating re-encrypts nothing retroactively — a rotation effectively starts a new repository) | See §6 — never rotate without first confirming the new password is recorded off-server |
| Email provider API key | Maintainer | Per provider's own guidance, at minimum yearly | Rotate via provider dashboard, update `.env` and the `EMAIL_PROVIDER_API_KEY` GitHub secret together, confirm a test send succeeds |
| `VPS_SSH_KEY` (deploy keypair) | Maintainer | Yearly, or on suspected compromise | See §4 |
| GHCR `read:packages` token | Maintainer | Set an explicit expiry when the token is created (fine-grained PATs support this); record the expiry date in the password manager entry and set a calendar reminder ~2 weeks before it lapses | Generate a new fine-grained PAT scoped to `read:packages` only, `docker login ghcr.io` again on the VPS as `deploy`, confirm `docker compose --profile app pull` still succeeds, then revoke the old token |

An expired GHCR token fails silently until the next deploy or restart tries to pull an image — the
calendar reminder above, not the pipeline, is what catches it.

## 6. Restic password: off-server storage

A backup encryption key that exists only on the machine it protects is not a backup — it is a single
point of failure. `RESTIC_PASSWORD` therefore lives in two places, never one:

1. `/opt/nimbus/.env` on the VPS (needed for `restic backup`/`restic forget` to run).
2. The maintainer's password manager, entered at the same time the `.env` value is generated in
   `infra/nimbus-issue-5-STEPS.md` Part D2 — copy it out **before** moving on, not after.

If the VPS is destroyed, the password manager copy is what makes the offsite `RESTIC_REPOSITORY`
snapshots recoverable at all. Losing both copies makes every existing snapshot permanently
unrecoverable, so treat the password-manager write as part of the generation step, not an optional
follow-up.

## 7. No secret baked into an image, logged, or committed

- Application images (`nimbus-api`, `nimbus-migrator`) receive configuration only via `env_file:
  [.env]` at container start — never via `ARG`/`ENV` baked in at build time, and no secret appears
  in a `Dockerfile`.
- Workflow steps that handle `${{ secrets.* }}` avoid `set -x`/`echo` on those values; GitHub
  Actions also automatically redacts any literal secret value that appears in step output.
- Secret-scanning runs in CI on every push and pull request —
  `.github/workflows/secret-scan.yml` (gitleaks) — and fails the build on a match.
- `infra/.env` is gitignored; `infra/.env.example` is the only committed variant and holds no real
  values.

## 8. Verifying "no default fallback" stays true

Because this is easy to regress one PR at a time, check it whenever `docker-compose.prod.yml` or
`docker-compose.limits.yml` changes:

```bash
grep -nE '\$\{[A-Z_]+:-' infra/docker-compose.prod.yml infra/docker-compose.limits.yml
```

The only acceptable match is `IMAGE_TAG:-latest` — anything else (e.g. a reintroduced
`GRAFANA_ADMIN_PASSWORD:-admin` or a hardcoded `MSSQL_PID: "Developer"`) must be removed before
merge.
