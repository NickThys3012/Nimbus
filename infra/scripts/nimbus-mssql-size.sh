#!/usr/bin/env bash
# Emits nimbus_mssql_database_size_bytes, read by node-exporter's textfile
# collector. Alerted on in infra/observability/alert.rules.yml (NimbusDatabaseApproachingExpressLimit)
# ahead of SQL Server Express's hard 10 GB per-database ceiling. Install as a
# systemd timer — see infra/VPS-SETUP.md#e3-install-the-host-side-observability-scripts-and-timers.
set -euo pipefail

OUT=/var/lib/node_exporter/textfile_collector/nimbus_mssql_size.prom
TMP="${OUT}.$$"

# size_on_disk_bytes sums the data files (ROWS) backing the Nimbus database —
# SQL Server Express's 10 GB limit applies to data files, not the log.
SIZE_BYTES=$(docker exec nimbus-sqlserver-1 /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P "${MSSQL_SA_PASSWORD}" -C -h -1 -W \
  -Q "SET NOCOUNT ON; SELECT SUM(size) * 8 * 1024 FROM sys.master_files WHERE database_id = DB_ID('Nimbus') AND type_desc = 'ROWS';" \
  2>/dev/null | tr -d '[:space:]')

{
  echo '# HELP nimbus_mssql_database_size_bytes Total on-disk size of the Nimbus SQL Server database.'
  echo '# TYPE nimbus_mssql_database_size_bytes gauge'
  echo "nimbus_mssql_database_size_bytes ${SIZE_BYTES:-0}"
} > "$TMP"
mv "$TMP" "$OUT"
