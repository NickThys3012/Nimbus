#!/usr/bin/env bash
# Emits nimbus_mssql_database_size_bytes, read by node-exporter's textfile
# collector. Alerted on in infra/alert.rules.yml (NimbusDatabaseApproachingExpressLimit)
# ahead of SQL Server Express's hard 10 GB per-database ceiling. Install as a
# systemd timer — see infra/DEPLOY-12.md.
set -euo pipefail

OUT=/var/lib/node_exporter/textfile_collector/nimbus_mssql_size.prom
TMP="${OUT}.$$"

# size_on_disk_bytes sums every data + log file backing the Nimbus database —
# the same figure SQL Server Express counts against its 10 GB data-file cap.
SIZE_BYTES=$(docker exec nimbus-sqlserver-1 /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P "${MSSQL_SA_PASSWORD}" -C -h -1 -W \
  -Q "SET NOCOUNT ON; SELECT SUM(size) * 8 * 1024 FROM sys.master_files WHERE database_id = DB_ID('Nimbus');" \
  2>/dev/null | tr -d '[:space:]')

{
  echo '# HELP nimbus_mssql_database_size_bytes Total on-disk size of the Nimbus SQL Server database.'
  echo '# TYPE nimbus_mssql_database_size_bytes gauge'
  echo "nimbus_mssql_database_size_bytes ${SIZE_BYTES:-0}"
} > "$TMP"
mv "$TMP" "$OUT"
