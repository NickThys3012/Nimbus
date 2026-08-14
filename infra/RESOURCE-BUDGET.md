# Resource Budget — RAM, CPU, Disk, and Restart Policy (issue #103)

Host: **6 vCPU / 12 GB RAM / 200 GB SSD** (Contabo VPS provisioned in #5).

Steady-state usage is roughly 3.5–4.5 GB against 12 GB available, so this host is comfortable rather
than tight. The limits in this document exist to contain spikes and mistakes, not to ration a scarce
resource.

## Memory and CPU ceilings

Enforced by `infra/compose/docker-compose.limits.yml`, deployed on the VPS as `compose.override.yaml` so
Compose merges it automatically alongside `compose.yaml` (see
[`VPS-SETUP.md`](VPS-SETUP.md#part-e-first-deploy)).

| Service | Memory limit | Reservation | CPUs |
|---|---|---|---|
| sqlserver | 2560M | 1536M | 3.0 |
| api | 2G | 512M | 3.0 |
| prometheus | 1536M | 384M | 1.0 |
| minio | 1G | 256M | 1.5 |
| loki | 1G | 256M | 1.0 |
| grafana | 512M | 128M | 0.5 |
| caddy | 256M | 64M | 1.0 |
| node-exporter | 128M | — | 0.25 |
| migrator (transient) | 512M | — | 2.0 |

**Memory:** the limits sum to ~8.9 GB against 12 GB physical RAM, leaving ~3.1 GB for the kernel,
page cache, SSH, cron and restic. That headroom is what makes these limits a real guarantee rather
than eight ceilings that can collectively overcommit the box. **Adding a service means taking memory
from another service's ceiling, not from the headroom.**

**CPU:** limits deliberately sum to 11.25, more than the 6 physical vCPUs. CPU is a share, not a
reservation, so oversubscription is correct: the API can burst into idle cores during a SkiaSharp
render while a runaway Prometheus query still cannot starve it.

### SQL Server memory

`MSSQL_MEMORY_LIMIT_MB=1792` (set in `.env`, sourced from `.env.example`) is comfortably below the
2560M container ceiling, so SQL Server backs off before the OOM killer intervenes. Express caps the
buffer pool near 1410 MB regardless of this setting, but the total process footprint (buffer pool +
plan cache + thread stacks + Express overhead) sits well above that — hence the wider 2560M ceiling.

### Native allocations (SkiaSharp / QuestPDF)

SkiaSharp and QuestPDF, used by the API for map renders and PDF export, allocate memory **outside
the .NET GC heap** (native/unmanaged allocations for image buffers, fonts, and rasterization).
No `DOTNET_GCHeapHardLimit`, `GCHeapCount`, or server-GC setting governs this memory — it is invisible
to the CLR. The **container's `mem_limit: 2g` is the only real guard** against a runaway render
(e.g. an oversized map export) taking down the API or the host. This is why the API's memory limit
is sized with headroom above typical .NET heap usage.

### Prometheus retention

Prometheus is the volatile one — its footprint scales with active series and retention, not traffic.
Retention and memory are decided together, not independently:

```
--storage.tsdb.retention.time=15d
--storage.tsdb.retention.size=8GB
```

(set in `docker-compose.prod.yml`). 15 days / 8 GB keeps Prometheus inside both its 1536M memory
ceiling and the ~10 GB disk budget below.

## Docker log rotation

Set daemon-wide in `/etc/docker/daemon.json` (committed at `infra/docker/daemon.json`, applied in
[`infra/VPS-SETUP.md`](VPS-SETUP.md#c5-install-docker-and-set-the-daemon-policy)), not per service,
so it cannot be forgotten on a service added later:

```json
{
  "log-driver": "json-file",
  "log-opts": { "max-size": "50m", "max-file": "3" },
  "live-restore": true
}
```

Worst case: 150 MB of logs per container × 9 services ≈ 1.35 GB, well inside the ~20 GB "OS and
images" disk budget below.

## Disk allocation (200 GB SSD)

Budgeted per volume, not just monitored on the root filesystem:

| Volume | Path | Budget | Monitoring |
|---|---|---|---|
| SQL Server data | `/srv/nimbus/data/mssql` | ~25 GB | `nimbus_directory_size_bytes{directory="mssql"}` |
| MinIO objects | `/srv/nimbus/data/minio` | ~100 GB | `nimbus_directory_size_bytes{directory="minio"}` |
| Loki chunks/index | `/srv/nimbus/data/loki` | ~20 GB | `nimbus_directory_size_bytes{directory="loki"}` |
| Prometheus TSDB | `/srv/nimbus/data/prometheus` | ~10 GB | `nimbus_directory_size_bytes{directory="prometheus"}` |
| OS, Docker images/layers, container logs | `/` (excl. `/srv/nimbus`) | ~20 GB | `node_filesystem_avail_bytes{mountpoint="/"}` |
| Backup staging (restic pre-upload) | `/srv/nimbus/backup-staging` | ~15 GB | `nimbus_directory_size_bytes{directory="backup-staging"}` |
| Unallocated headroom | — | ~10 GB | — |
| **Total** | | **200 GB** | |

`nimbus_directory_size_bytes` is emitted by a textfile-collector script (cron job scraping `du -sb`
per directory into `/var/lib/node_exporter/textfile_collector`, read by `node-exporter`) and alerted
on in `infra/observability/alert.rules.yml` (`NimbusDiskFillingUp`, `NimbusDirectoryGrowthAnomaly`).

**The one genuinely variable number is MinIO's disk.** 100–200 GB is generous for a single-pilot
logbook, but media accumulates and nothing prunes it automatically — that's what issue #78's orphan
cleanup addresses.

## Upload-size ceiling

A documented ceiling of **100 MB per upload** is enforced at two layers, so one large file cannot
exhaust the disk:

1. **Caddy** — `request_body { max_size 100MB }` on the `nimbus.$NIMBUS_DOMAIN` site
   (`infra/caddy/Caddyfile`). Requests exceeding this are rejected before reaching the API.
2. **API (Kestrel)** — `KestrelServerOptions.Limits.MaxRequestBodySize = 100 * 1024 * 1024` set in
   `Nimbus.API/Nimbus.API/Program.cs`. This is the defense-in-depth layer behind Caddy; individual
   upload endpoints can additionally apply `[RequestSizeLimit]` for a tighter, endpoint-specific cap.
   Keep both numbers in sync if the ceiling ever changes.

## Verification checklist (issue #103)

The following require a live VPS and are **not yet performed** — track them as follow-up work before
closing #103:

- [ ] **Load test:** run a concurrent burst of PDF exports and map renders against the deployed
      stack; confirm via `docker stats api` / Grafana that the API stays within its 2G limit instead
      of being OOM-killed (`docker inspect --format='{{.State.OOMKilled}}' <container>` should stay
      `false`).
- [ ] **Reboot verification:** `sudo reboot` the VPS, then confirm `docker compose ps` shows every
      service `running`/`healthy` again without manual intervention (all services already carry
      `restart: unless-stopped` in `docker-compose.prod.yml`, and `docker` itself is enabled via
      `systemctl enable docker`, so this should be a no-touch recovery).
