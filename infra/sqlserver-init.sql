-- Least-privilege SQL Server bootstrap for the Nimbus production stack (issue #2).
--
-- Runs once per `docker compose up` via the `sqlserver-init` service, authenticating
-- with `sa` for this bootstrap only. It:
--   1. creates the `Nimbus` database if it does not already exist
--   2. creates a `nimbus_app` login/user the API authenticates with — db_datareader
--      and db_datawriter only, no DDL rights, never `sa`
--   3. creates a `nimbus_migrator` login/user the `migrator` container authenticates
--      with — db_owner (DDL rights), used only for the lifetime of the transient
--      migrator container, never by the long-running API
--
-- Every statement is guarded so a re-run (redeploy, container restart) is a no-op
-- rather than an error — mirrors the idempotency pattern in infra/minio-init.sh.
IF DB_ID(N'Nimbus') IS NULL
BEGIN
    CREATE DATABASE [Nimbus];
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'nimbus_app')
BEGIN
    CREATE LOGIN [nimbus_app] WITH PASSWORD = N'$(AppPassword)', CHECK_POLICY = ON;
END
ELSE
BEGIN
    ALTER LOGIN [nimbus_app] WITH PASSWORD = N'$(AppPassword)';
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'nimbus_migrator')
BEGIN
    CREATE LOGIN [nimbus_migrator] WITH PASSWORD = N'$(MigratorPassword)', CHECK_POLICY = ON;
END
ELSE
BEGIN
    ALTER LOGIN [nimbus_migrator] WITH PASSWORD = N'$(MigratorPassword)';
END;
GO

USE [Nimbus];
GO

IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'nimbus_app')
BEGIN
    CREATE USER [nimbus_app] FOR LOGIN [nimbus_app];
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'nimbus_migrator')
BEGIN
    CREATE USER [nimbus_migrator] FOR LOGIN [nimbus_migrator];
END;
GO

-- The API never needs DDL rights, so it never gets them — read/write on rows only.
IF IS_ROLEMEMBER(N'db_datareader', N'nimbus_app') <> 1
    ALTER ROLE db_datareader ADD MEMBER [nimbus_app];
IF IS_ROLEMEMBER(N'db_datawriter', N'nimbus_app') <> 1
    ALTER ROLE db_datawriter ADD MEMBER [nimbus_app];
GO

-- db_owner is scoped to this database only, and only the transient `migrator`
-- container ever authenticates as `nimbus_migrator` — the API never does.
ALTER ROLE db_owner ADD MEMBER [nimbus_migrator];
GO
