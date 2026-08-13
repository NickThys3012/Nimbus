# syntax=docker/dockerfile:1
#
# Two independently deployable images come out of this file (issue #2):
#   - `api`      the ASP.NET Core application, no migration responsibility at all.
#   - `migrator` a self-contained `dotnet ef migrations bundle` executable that
#                applies pending EF Core migrations and exits — nothing here reads
#                or writes application code, so it is safe to run as a one-shot
#                `depends_on: condition: service_completed_successfully` step ahead
#                of `api` (see infra/docker-compose.prod.yml).
#
# Build context is the repository root:
#   docker build --target api      -t nimbus-api .
#   docker build --target migrator -t nimbus-migrator .
#
# No secret is ever passed as a build ARG/ENV — both images read their connection
# string from the environment at container start (docs/configuration.md §7).

# ---------------------------------------------------------------- web (Angular)
FROM node:22-alpine AS web-build
WORKDIR /src/Nimbus.Web
COPY Nimbus.Web/package.json Nimbus.Web/package-lock.json ./
RUN npm ci
COPY Nimbus.Web/ ./
RUN npm run build -- --configuration production

# --------------------------------------------------------------- .NET restore
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS dotnet-base
WORKDIR /src
COPY Nimbus.API/Nimbus.slnx ./Nimbus.API/
COPY Nimbus.API/Nimbus.Domain/Nimbus.Domain.csproj ./Nimbus.API/Nimbus.Domain/
COPY Nimbus.API/Nimbus.Application/Nimbus.Application.csproj ./Nimbus.API/Nimbus.Application/
COPY Nimbus.API/Nimbus.Contracts/Nimbus.Contracts.csproj ./Nimbus.API/Nimbus.Contracts/
COPY Nimbus.API/Nimbus.Infrastructure/Nimbus.Infrastructure.csproj ./Nimbus.API/Nimbus.Infrastructure/
COPY Nimbus.API/Nimbus.Logging/Nimbus.Logging.csproj ./Nimbus.API/Nimbus.Logging/
COPY Nimbus.API/Nimbus.Observability/Nimbus.Observability.csproj ./Nimbus.API/Nimbus.Observability/
COPY Nimbus.API/Nimbus.API/Nimbus.API.csproj ./Nimbus.API/Nimbus.API/
RUN dotnet restore Nimbus.API/Nimbus.API/Nimbus.API.csproj
COPY Nimbus.API/ ./Nimbus.API/

# ------------------------------------------------------------------ api build
FROM dotnet-base AS api-build
# The Angular app is built above (web-build), not by MSBuild's own npm target,
# so publishing here is a plain `dotnet publish` with no Node.js in this stage.
RUN dotnet publish Nimbus.API/Nimbus.API/Nimbus.API.csproj \
    -c Release -o /app/publish \
    -p:SkipSpaBuild=true
COPY --from=web-build /src/Nimbus.Web/dist/Nimbus.Web/browser/ /app/publish/wwwroot/

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS api
WORKDIR /app
RUN useradd --no-create-home --uid 10001 nimbus
USER nimbus
COPY --from=api-build --chown=nimbus:nimbus /app/publish ./
ENTRYPOINT ["dotnet", "Nimbus.API.dll"]

# ------------------------------------------------------------- migrator build
FROM dotnet-base AS migrator-build
RUN dotnet tool install --global dotnet-ef --version 10.0.10
ENV PATH="$PATH:/root/.dotnet/tools"
# A self-contained bundle needs RID-specific packages restored up front —
# the shared `dotnet-base` restore above is portable (no -r), so restore again here.
RUN dotnet restore Nimbus.API/Nimbus.API/Nimbus.API.csproj -r linux-x64
# A migration bundle is a self-contained executable purpose-built to apply
# migrations and exit (dotnet/efcore docs: "Applying migrations in production");
# it needs neither the SDK nor the ASP.NET Core app at runtime.
RUN dotnet ef migrations bundle \
    --project Nimbus.API/Nimbus.Infrastructure/Nimbus.Infrastructure.csproj \
    --startup-project Nimbus.API/Nimbus.API/Nimbus.API.csproj \
    --configuration Release \
    --self-contained -r linux-x64 \
    --output /app/efbundle

FROM mcr.microsoft.com/dotnet/runtime-deps:10.0 AS migrator
WORKDIR /app
RUN useradd --no-create-home --uid 10001 nimbus
USER nimbus
COPY --from=migrator-build --chown=nimbus:nimbus /app/efbundle ./efbundle
# `ConnectionStrings__Database` is the same config key the API binds
# (Nimbus.Infrastructure/DependencyInjection.cs), so both images read the
# connection string from the same environment variable — just different
# credentials (see infra/sqlserver-init.sql: nimbus_migrator vs nimbus_app).
# Exec-form ENTRYPOINT does not expand env vars, hence the shell wrapper.
ENTRYPOINT ["/bin/sh", "-c", "exec ./efbundle --connection \"$ConnectionStrings__Database\""]
