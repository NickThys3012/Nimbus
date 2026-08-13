# Deploying issue #94 (production compose stack hardening) to the VPS

`docker-compose.prod.yml` and `Caddyfile` already carried most of #94's acceptance criteria from
earlier issues (#2, #5, #11, #12, #95, #96, #103). This guide covers the small remaining delta:
the `migrator` restart policy, the API's `depends_on` on MinIO, and Caddy security headers.

## What changed

| File | Change | Why |
|---|---|---|
| `docker-compose.prod.yml` | `migrator` restart policy `on-failure` → `"no"` | `migrator` is a one-shot, transient container (`profiles: ["app"]`, gated by `service_completed_successfully`). `on-failure` would silently retry a failed migration forever instead of surfacing it — a bad migration should be investigated and re-run deliberately with `docker compose --profile app up migrator`, matching the `sqlserver-init`/`minio-init` one-shot jobs which already used `restart: "no"`. |
| `docker-compose.prod.yml` | `api` now `depends_on: minio: condition: service_healthy` (in addition to the existing `sqlserver`, `migrator`, `minio-init` conditions) | The acceptance criteria call for the API to wait on both the database *and* MinIO with `service_healthy`. It previously only waited on `minio-init`'s completion, which depends on `minio` transitively — this makes the dependency explicit rather than incidental. |
| `docker-compose.prod.yml` | Added a comment above `caddy.ports` | Documents *why* only Caddy publishes ports (Docker's iptables rules are evaluated before ufw's) and calls out that a separate dev compose file — not this one — is what publishes SQL Server/Grafana/Prometheus/Loki ports for local debugging, so the omission here reads as deliberate. |
| `Caddyfile` | Added a `(security_headers)` snippet (HSTS, `X-Content-Type-Options: nosniff`, `Referrer-Policy: strict-origin-when-cross-origin`) imported into all three site blocks | Acceptance criteria required these headers; they were missing entirely before. |

Everything else in the acceptance criteria (`restart: unless-stopped` on every long-running
service, no `ports:` on anything but Caddy, the prod image reference from GHCR with no `build:`
key, `depends_on` conditions on `sqlserver`, bind-mounted persistent state under
`/srv/nimbus/data/*` for every stateful service so `docker compose down` destroys nothing, and the
Caddyfile reverse-proxying the hostname to the API plus separate Grafana/MinIO-console routes) was
already in place from #2/#5/#11/#12/#95/#96/#103 — see those issues' `DEPLOY-*.md` files for how
each piece was rolled out originally.

> **Note on volumes:** the acceptance criteria describe "named volumes"; this stack instead uses
> host bind mounts under `/srv/nimbus/data/*` (decided in issue #5, see
> `infra/nimbus-issue-5-STEPS.md`). Functionally this satisfies the actual requirement —
> `docker compose down` never touches files outside the compose project, named volume or bind
> mount — and bind mounts are what `restic` (see `RESOURCE-BUDGET.md`) backs up directly by path.
> No change was made here; flagging it so the deviation reads as deliberate, not overlooked.

## 1. Copy the changed files `[LOCAL]`

```bash
cd ~/path/to/Nimbus
scp infra/docker-compose.prod.yml deploy@<vps-ip>:/opt/nimbus/compose.yaml
scp infra/Caddyfile deploy@<vps-ip>:/opt/nimbus/
```

No `.env` or secret changes are needed for this issue.

## 2. Verify the merged config before touching anything running `[VPS]`

```bash
cd /opt/nimbus
docker compose --profile app config -q && echo "CONFIG OK"
docker compose --profile app config | grep -A2 'migrator:' | grep restart
# expect: restart: "no"
```

## 3. Roll out `[VPS]`

```bash
cd /opt/nimbus
docker compose --profile app up -d caddy api migrator
docker compose --profile app ps
```

`migrator` should show `exited (0)` (one-shot); `caddy` and `api` should read `running`/`healthy`.

## 4. Confirm the security headers are live `[VPS]`

```bash
curl -sI https://<your-domain> | grep -Ei 'strict-transport-security|x-content-type-options|referrer-policy'
```

Expect all three headers present with the values from the table above.

## 5. Confirm the API waits on MinIO `[VPS]`

```bash
docker compose --profile app config | grep -B1 -A6 '^  api:' | grep -A6 depends_on
```

Expect `sqlserver`, `minio`, `migrator`, and `minio-init` all listed with their respective
conditions.

## 6. Blank-server end-to-end check (do this once, then record it)

Per the last acceptance criterion, bring the stack up on a **blank** server using only
`compose.yaml`, `Caddyfile`, and a freshly populated `.env` — no manual state carried over from a
previous deploy. Follow `infra/nimbus-issue-5-STEPS.md` from the top on a throwaway VPS/VM,
confirm every service reaches `running`/`healthy` and the site is reachable over HTTPS with a
valid Let's Encrypt certificate, then record the date and outcome here:

- [ ] Blank-server bring-up verified on: _____________ (date), result: _____________

## Troubleshooting

| Symptom | Cause | Fix |
|---|---|---|
| `api` never leaves `starting`/`created` | `minio` unhealthy, so the new `depends_on` condition blocks `api` | `docker compose logs minio`; confirm `MINIO_ROOT_USER`/`MINIO_ROOT_PASSWORD` are set in `.env` |
| `migrator` restarts repeatedly after a bad migration | Old `on-failure` policy still cached from a prior `up` | `docker compose --profile app up -d --force-recreate migrator` after pulling this compose file |
| Security headers missing from `curl -I` output | Caddy still running with the old config | `scp` step 1 not done, or `docker compose up -d caddy` not re-run; check `docker compose logs caddy` for reload errors |
