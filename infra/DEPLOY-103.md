# Deploying issue #103 (resource limits) to the VPS

This is the delta on top of `nimbus-issue-5-STEPS.md` Part D — you already have `/opt/nimbus`
provisioned and the stack reachable. This guide only covers rolling out the new resource-limit
files. No CI pipeline publishes `nimbus-api` / `nimbus-migrator` images yet, so `--profile app`
will fail with `denied` (image doesn't exist in GHCR). Use `--profile stub` until that pipeline
exists.

## 1. Copy the new/changed files `[LOCAL]`

```bash
cd ~/path/to/Nimbus
scp infra/docker-compose.prod.yml deploy@<vps-ip>:/opt/nimbus/compose.yaml
scp infra/docker-compose.limits.yml deploy@<vps-ip>:/opt/nimbus/compose.override.yaml
scp infra/Caddyfile infra/prometheus.yml infra/alert.rules.yml deploy@<vps-ip>:/opt/nimbus/
scp infra/daemon.json deploy@<vps-ip>:~/daemon.json
```

`compose.override.yaml` is auto-merged by Compose alongside `compose.yaml` — no `-f` flags needed
in any command below.

## 2. Update the Docker daemon `[VPS]`

Log rotation changed from `max-size: 10m` to `50m` (issue #103):

```bash
sudo cp ~/daemon.json /etc/docker/daemon.json
sudo systemctl restart docker
```

## 3. Update `.env` `[VPS]`

```bash
sudo sed -i 's/^MSSQL_MEMORY_LIMIT_MB=.*/MSSQL_MEMORY_LIMIT_MB=1792/' /opt/nimbus/.env
grep MSSQL_MEMORY_LIMIT_MB /opt/nimbus/.env   # confirm it says 1792
```

## 4. Verify the merged config before touching anything running `[VPS]`

```bash
cd /opt/nimbus
docker compose --profile stub config > /dev/null && echo "CONFIG OK"
docker compose --profile stub config | grep -A2 mem_limit
```

Confirm every service shows the `mem_limit`/`mem_reservation`/`cpus` values from the table in
`infra/RESOURCE-BUDGET.md`.

## 5. Roll out `[VPS]`

```bash
cd /opt/nimbus
docker compose --profile stub pull    # only pulls real images: caddy, sqlserver, minio, loki, prometheus, grafana, node-exporter
docker compose --profile stub up -d
docker compose --profile stub ps      # confirm every service is "running"
```

`api-stub` (`traefik/whoami`) answers as `api` until real images exist — nothing in Caddyfile or the
compose file needs to change later. Switch to `docker compose --profile app up -d` once CI publishes
`ghcr.io/nickthys3012/nimbus-api` and `nimbus-migrator`.

## 6. Spot-check the limits are real, not just configured `[VPS]`

```bash
docker stats --no-stream
docker inspect sqlserver --format '{{.HostConfig.Memory}} {{.HostConfig.NanoCpus}}'
```

`Memory` should read `2684354560` (2560M) for `sqlserver`, and similarly for other services per the
table.

## 7. Two verifications that still require the live box (open items on #103)

- **Load test** — once the real `api` image is deployed (`--profile app`), fire a concurrent burst
  of PDF exports and map renders, then check:
  ```bash
  docker inspect --format='{{.State.OOMKilled}}' <api-container-id>   # must stay false
  docker stats --no-stream api
  ```
- **Reboot verification**:
  ```bash
  sudo reboot
  # after it comes back:
  ssh deploy@<vps-ip> 'docker compose -C /opt/nimbus --profile stub ps'
  ```
  Every service should be back up unattended — `restart: unless-stopped` plus
  `systemctl enable docker` (already set in Part C5) handle this with no manual steps.

## Troubleshooting

| Symptom | Cause | Fix |
|---|---|---|
| `Error response from daemon: error from registry: denied` on `--profile app` | No CI workflow publishes `nimbus-api`/`nimbus-migrator` to GHCR yet | Use `--profile stub` instead, or build+push the images manually first |
| `docker compose config` shows old `mem_limit: 5g` for sqlserver | `compose.override.yaml` wasn't copied, or was copied with the wrong name | Re-run step 1, confirm `ls /opt/nimbus` shows `compose.override.yaml` |
| SQL Server container restarts in a loop after step 3 | `MSSQL_MEMORY_LIMIT_MB` set above the new 2560M ceiling in some stale `.env` | Confirm `.env` says `1792`, not `4096` |
