#!/bin/sh
# Idempotent SQL Server bootstrap for the Nimbus production stack (issue #2).
#
# Runs once per `docker compose up` via the `sqlserver-init` service — the same
# `mssql/server` image already on the box, so `sqlcmd` is available with no
# extra image to pull. Authenticates with `sa` for this bootstrap only, then
# creates the least-privilege `nimbus_app` / `nimbus_migrator` logins the
# application containers actually connect as (see infra/sqlserver-init.sql).
#
# Safe to re-run: every statement in the .sql file is guarded with an
# existence check or is a plain ALTER, so a rebuild never errors on a login
# that already exists.
set -eu

SQLCMD=/opt/mssql-tools18/bin/sqlcmd

echo "Waiting for SQL Server at ${MSSQL_HOST:-sqlserver}..."
until "${SQLCMD}" -S "${MSSQL_HOST:-sqlserver}" -U sa -P "${MSSQL_SA_PASSWORD}" -C -Q "SELECT 1" >/dev/null 2>&1; do
	sleep 2
done
echo "SQL Server is reachable."

"${SQLCMD}" -S "${MSSQL_HOST:-sqlserver}" -U sa -P "${MSSQL_SA_PASSWORD}" -C \
	-v AppPassword="${MSSQL_APP_PASSWORD}" MigratorPassword="${MSSQL_MIGRATOR_PASSWORD}" \
	-i /init/sqlserver-init.sql

echo "SQL Server bootstrap complete: database=Nimbus app-login=nimbus_app migrator-login=nimbus_migrator"
