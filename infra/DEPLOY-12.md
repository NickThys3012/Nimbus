# Deploying issue #12 (structured logging, metrics, traces) to the VPS

This is the delta on top of `nimbus-issue-5-STEPS.md` Part D and `DEPLOY-103.md` — Loki, Prometheus,
Grafana and node-exporter are already running as bare containers. This guide covers what issue #12
adds on top: explicit Loki retention, Grafana provisioning, the extra alert rules and the host-side
scripts that feed them, and the client telemetry endpoint. No new container is introduced — still
three observability services plus node-exporter, per the issue's "three containers, not six" note.

## 1. Copy the new/changed files `[LOCAL]`

```bash
cd ~/path/to/Nimbus
scp infra/docker-compose.prod.yml infra/alert.rules.yml infra/loki-config.yaml deploy@<vps-ip>:/opt/nimbus/
scp -r infra/grafana deploy@<vps-ip>:/opt/nimbus/
scp -r infra/scripts deploy@<vps-ip>:~/nimbus-scripts/
```

`docker-compose.prod.yml` now mounts `./loki-config.yaml` into the `loki` service and
`./grafana/provisioning` + `./grafana/dashboards` into the `grafana` service — both are relative to
`/opt/nimbus`, matching the existing `alert.rules.yml`/`prometheus.yml` bind mounts.

## 2. Update `.env` `[VPS]`

`Loki__Url` replaces the previous `LOKI_URL` — ASP.NET Core's environment-variable configuration
provider only recognizes **double** underscores as a section separator (`Loki__Url` →
configuration key `Loki:Url`); `LOKI_URL` silently bound to nothing, leaving the Loki sink disabled.

```bash
sudo sed -i 's/^LOKI_URL=.*/Loki__Url=http:\/\/loki:3100/' /opt/nimbus/.env
grep '^Loki__Url=' /opt/nimbus/.env   # confirm it says Loki__Url=http://loki:3100
```

`GRAFANA_ADMIN_PASSWORD` must already be set with no default fallback (see `docs/configuration.md`)
— confirm there is no `${GRAFANA_ADMIN_PASSWORD:-admin}` anywhere in the compose file before rolling
out; the production compose file only ever references `${GRAFANA_ADMIN_PASSWORD}`.

## 3. Install the host-side textfile-collector scripts `[VPS]`

These feed the alert rules that need a metric node-exporter cannot produce on its own:
`nimbus_cert_expiry_seconds`, `nimbus_container_restart_count`, `nimbus_mssql_database_size_bytes`,
`nimbus_backup_last_success_timestamp`. Follow the same pattern as `nimbus-dirsize.sh` in
`nimbus-issue-5-STEPS.md` (Directory-size metric section) — one systemd oneshot service + timer per
script, writing to `/var/lib/node_exporter/textfile_collector/`.

```bash
sudo install -m 755 ~/nimbus-scripts/nimbus-cert-expiry.sh /usr/local/bin/
sudo install -m 755 ~/nimbus-scripts/nimbus-container-restarts.sh /usr/local/bin/
sudo install -m 755 ~/nimbus-scripts/nimbus-mssql-size.sh /usr/local/bin/
```

For each script, create a `systemd` service + timer pair (substitute the script name):

```bash
sudo tee /etc/systemd/system/nimbus-cert-expiry.service > /dev/null <<'EOF'
[Unit]
Description=Export Caddy certificate expiry as a Prometheus metric

[Service]
Type=oneshot
ExecStart=/usr/local/bin/nimbus-cert-expiry.sh
EOF

sudo tee /etc/systemd/system/nimbus-cert-expiry.timer > /dev/null <<'EOF'
[Unit]
Description=Run nimbus-cert-expiry hourly

[Timer]
OnCalendar=hourly
Persistent=true

[Install]
WantedBy=timers.target
EOF

sudo systemctl daemon-reload
sudo systemctl enable --now nimbus-cert-expiry.timer
```

`nimbus-container-restarts.sh` should run every 1–2 minutes (`OnCalendar=*:0/2`) so
`NimbusContainerRestartLoop`'s 15-minute `increase()` window has enough samples. `nimbus-mssql-size.sh`
needs `MSSQL_SA_PASSWORD` exported into its systemd unit (`EnvironmentFile=/opt/nimbus/.env`) since it
runs `sqlcmd` inside the `sqlserver` container.

`nimbus-backup-success.sh` is **not** a timer — it is the last step of the nightly backup job itself
(issue #99, not yet built), called only after `restic backup && restic check` both succeed. Wire it in
when #99 lands; until then `NimbusBackupStale` will fire immediately (no metric yet exists), which is
expected and can be silenced/ignored.

## 4. Verify the merged config `[VPS]`

```bash
cd /opt/nimbus
docker compose --profile stub config > /dev/null && echo "CONFIG OK"
docker compose --profile stub config | grep -A3 'loki-config.yaml\|provisioning'
```

## 5. Roll out `[VPS]`

```bash
cd /opt/nimbus
docker compose --profile stub up -d loki grafana prometheus
docker compose --profile stub logs loki --tail 50      # confirm it started with the new config
docker compose --profile stub logs grafana --tail 50   # confirm datasources/dashboards provisioned without error
```

Open `https://grafana.<domain>` and confirm: logging in with `GRAFANA_ADMIN_USER`/`_PASSWORD` from
`.env` works, the **Prometheus** and **Loki** datasources exist without needing to be added by hand,
and the **Nimbus** dashboard folder contains the "Nimbus API Overview" dashboard.

## What issue #12 does *not* add here

- **Tracing (Tempo/OpenTelemetry)** and **cadvisor** — explicitly deferred in the issue, each with its
  own trigger condition, not a Sprint 0 deliverable.
- **Alertmanager / notification routing** — the alert rules fire and are visible in Prometheus's own
  `/alerts` UI and (once wired) Grafana, but nothing pages anyone yet. Same status as noted in
  `nimbus-issue-5-STEPS.md`: "these fire in Prometheus but notify nobody."
- **A shipper for non-API container logs** (Caddy, SQL Server, MinIO) — stays in `docker logs` only,
  per the issue's own notes. Promtail/Alloy is a reasonable later addition, not part of this issue.
