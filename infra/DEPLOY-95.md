# Deploying issue #95 (MinIO object store) to the VPS

This is the delta on top of `nimbus-issue-5-STEPS.md` Part D and `infra/DEPLOY-103.md` — you already
have `/opt/nimbus` provisioned and the stack reachable, and the #103 resource-limits rollout done.
This guide only covers the new MinIO bucket/policy/console pieces from issue #95.

## 1. Copy the new/changed files `[LOCAL]`

```bash
cd ~/path/to/Nimbus
scp infra/docker-compose.prod.yml deploy@<vps-ip>:/opt/nimbus/compose.yaml
scp infra/Caddyfile deploy@<vps-ip>:/opt/nimbus/
scp infra/minio-init.sh deploy@<vps-ip>:/opt/nimbus/
```

`docker-compose.limits.yml` (`compose.override.yaml` on the server) is unchanged by this issue — no
need to re-copy it unless you haven't already from #103.

## 2. Generate the new secrets `[VPS]`

Two new credential pairs, both distinct from the MinIO root pair you already generated in D2:

```bash
echo "MINIO_APP_ACCESS_KEY: nimbus-app"
echo "MINIO_APP_SECRET_KEY: $(openssl rand -base64 24)"
echo "MINIO_CONSOLE_PASSWORD (plaintext, for your password manager): $(openssl rand -base64 18)"
docker run --rm caddy:2-alpine caddy hash-password --plaintext '<paste the console password above>'
```

The last command prints a bcrypt hash — that hash (not the plaintext) is what goes in `.env`.

> **Escape every `$` as `$$` when you paste the hash into `.env`.** Docker Compose interpolates
> `${VAR}` in the compose file, and it *also* treats literal `$` characters inside an env-file value
> as its own (unset) variable references — e.g. `$2a$14$abc...` silently loses the `$14$abc...`
> fragment unless written as `$$2a$$14$$abc...`. Verify it after step 3 with:
> ```bash
> docker compose config | grep MINIO_CONSOLE_PASSWORD_HASH   # should show the $$-escaped value
> docker exec nimbus-caddy-1 sh -c 'echo $MINIO_CONSOLE_PASSWORD_HASH'  # should show the ORIGINAL, un-escaped hash
> ```
> If the second command doesn't match the hash you generated, the login will always return `401` even
> with the correct password.

## 3. Update `/opt/nimbus/.env` `[VPS]`

Remove the old unused line and add the four new variables:

```bash
cd /opt/nimbus
sed -i '/^MINIO_BUCKET=/d' .env

cat >> .env <<'EOF'
MINIO_APP_ACCESS_KEY=nimbus-app
MINIO_APP_SECRET_KEY=<paste MINIO_APP_SECRET_KEY>
MINIO_CONSOLE_USER=<pick a console login name>
MINIO_CONSOLE_PASSWORD_HASH=<paste the bcrypt hash from step 2, with every $ doubled to $$>
EOF

chmod 600 .env
grep -E '^MINIO_' .env   # sanity-check all five MINIO_* vars are present and non-empty
```

## 4. Point `console.$NIMBUS_DOMAIN` at your DNS `[LOCAL/DNS]`

Add an `A`/`AAAA` record for `console.<your-domain>` pointing at the VPS, same as you already did for
the bare domain and `grafana.<your-domain>` in Part B. Caddy requests a cert for it automatically on
first request — no manual cert steps.

## 5. Verify the merged config before touching anything running `[VPS]`

```bash
cd /opt/nimbus
docker compose --profile stub config > /dev/null && echo "CONFIG OK"
docker compose --profile stub config | grep -A3 minio-init
```

Confirm `MINIO_APP_ACCESS_KEY`/`MINIO_APP_SECRET_KEY` show real values, not empty strings — an empty
`${VAR}` here means step 3 was missed.

## 6. Roll out `[VPS]`

```bash
cd /opt/nimbus
docker compose --profile stub up -d          # picks up the new minio healthcheck + Caddy console route
docker compose --profile stub up minio-init  # one-shot: creates buckets, policy, app user — exits 0
docker compose --profile stub ps
```

`minio-init` correctly shows `exited (0)` once done — it's not meant to keep running. Everything else
(`api-stub`, `caddy`, `grafana`, `loki`, `minio`, `node-exporter`, `prometheus`, `sqlserver`) should
read `running`/`healthy`.

## 7. Confirm it actually took effect `[VPS]`

```bash
docker compose logs minio-init | tail -20
```

You want to see:
```
Bucket created successfully `local/flight-images`.
local/flight-images versioning is enabled
Access permission for `local/flight-images` is set to `private`
...
Created policy `nimbus-app` successfully.
Added user `nimbus-app` successfully.
```
(repeated for `flight-tracks`, `flight-exports`, `map-cache`; the username matches whatever
`MINIO_APP_ACCESS_KEY` you set in step 3).

Check the console route is live and behind auth:

```bash
curl -sI https://console.<your-domain> | head -1   # expect 401 without credentials
```

## 8. Verify anonymous access is actually denied `[VPS]`

Run this from inside the compose network, not from your Mac, so it hits MinIO directly:

```bash
docker compose exec minio sh -c "true"   # confirms the container name to target below
docker run --rm --network nimbus_nimbus curlimages/curl:latest \
  -s -o /dev/null -w "%{http_code}\n" http://minio:9000/flight-images/
```

Expect `403`. If you get `200`, stop and re-check `minio-init.sh` ran (step 6) before doing anything
else.

## 9. Re-run idempotency check (optional but recommended once) `[VPS]`

```bash
docker compose --profile stub up minio-init
```

Should complete cleanly a second time with the same output and exit `0` — confirms a future rebuild
needs no console clicking.

## 10. Restore-from-backup test — do this once, then record it

This is the one criterion of #95 that requires deliberately breaking and restoring the live volume.
Follow the full runbook in `infra/MINIO.md` under **"Restoring from backup"**. Once done, update the
outcome line at the bottom of that file with the date and result.

## Troubleshooting

| Symptom | Cause | Fix |
|---|---|---|
| `minio-init` exits non-zero on "Waiting for MinIO API" forever | `minio` container unhealthy or `MC_HOST_local` misconfigured | `docker compose logs minio`; confirm `MINIO_ROOT_USER`/`MINIO_ROOT_PASSWORD` are set in `.env` |
| `console.<domain>` gives a Caddy TLS/ACME error | DNS record missing or not yet propagated | Add/check the `A` record from step 4, retry after propagation |
| `curl` to `console.<domain>` returns 200 without a password prompt | `MINIO_CONSOLE_USER`/`MINIO_CONSOLE_PASSWORD_HASH` empty in `.env` | Re-check step 3, re-run `docker compose --profile stub up -d caddy` |
| Anonymous `curl` test in step 8 returns `200` | `minio-init` never ran, or ran before buckets existed | Re-run `docker compose --profile stub up minio-init` |
| API can't authenticate once real images are deployed | `MINIO_APP_ACCESS_KEY`/`MINIO_APP_SECRET_KEY` in the API's env don't match what `minio-init` created | Confirm the same `.env` is used by both `minio-init` and the `api` service (`env_file: [.env]`) |
