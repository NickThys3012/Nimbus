#!/usr/bin/env bash
# Records a successful backup as nimbus_backup_last_success_timestamp (a Unix
# timestamp gauge), read by node-exporter's textfile collector and alerted on
# in infra/observability/alert.rules.yml (NimbusBackupStale).
#
# The nightly backup job itself is issue #99 (not yet built) — call this script
# as the very last step of that job, only after `restic backup` / `restic check`
# both exit 0. Do not call it unconditionally from cron; a stale timestamp is
# the entire point of the alert, so a script that "records success" regardless
# of outcome would defeat it.
set -euo pipefail

OUT=/var/lib/node_exporter/textfile_collector/nimbus_backup.prom
TMP="${OUT}.$$"

{
  echo '# HELP nimbus_backup_last_success_timestamp Unix time of the last successful restic backup.'
  echo '# TYPE nimbus_backup_last_success_timestamp gauge'
  echo "nimbus_backup_last_success_timestamp $(date +%s)"
} > "$TMP"
mv "$TMP" "$OUT"
