#!/usr/bin/env bash
# Emits nimbus_container_restart_count{container=...}, read by node-exporter's
# textfile collector. Alerted on in infra/observability/alert.rules.yml (NimbusContainerRestartLoop),
# which looks for the counter climbing within a 15-minute window. Install as a
# systemd timer running every 1-2 minutes — see infra/VPS-SETUP.md#e3-install-the-host-side-observability-scripts-and-timers.
set -euo pipefail

OUT=/var/lib/node_exporter/textfile_collector/nimbus_container_restarts.prom
TMP="${OUT}.$$"

{
  echo '# HELP nimbus_container_restart_count Docker RestartCount per Nimbus container.'
  echo '# TYPE nimbus_container_restart_count counter'
  for name in $(docker compose -f /opt/nimbus/compose.yaml ps --format '{{.Name}}' 2>/dev/null); do
    count=$(docker inspect --format='{{.RestartCount}}' "$name" 2>/dev/null || echo 0)
    echo "nimbus_container_restart_count{container=\"${name}\"} ${count}"
  done
} > "$TMP"
mv "$TMP" "$OUT"
