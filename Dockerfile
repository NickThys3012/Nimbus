# syntax=docker/dockerfile:1
#
# Two independently deployable images come out of this file (issue #2):
#   - `api`      the ASP.NET Core application, no migration responsibility at all.
#   - `migrator` a self-contained `dotnet ef migrations bundle` executable that
#                applies pending EF Core migrations and exits — nothing here reads
#                or writes application code, so it is safe to run as a one-shot
#                `depends_on: condition: service_completed_successfully` step ahead
#                of `api` (see infra/compose/docker-compose.prod.yml).
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
# Baked in at build time (issue #96), not read from the environment at start-up,
# so the in-app changelog can never drift from the image actually running.
COPY release-notes.json /app/publish/wwwroot/release-notes.json

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS api
WORKDIR /app
# Trajectory/PDF map rendering (#57, #62) uses SkiaSharp. The NuGet package
# (SkiaSharp.NativeAssets.Linux.NoDependencies) statically links freetype/
# fontconfig/harfbuzz, so no extra apt packages are needed here — the dynamically
# linked variant hit undefined-symbol failures against this image's fontconfig/
# libuuid ABI, which is exactly the class of bug this build-time smoke test exists
# to catch before it reaches production.
RUN useradd --no-create-home --uid 10001 nimbus
COPY --from=api-build --chown=nimbus:nimbus /app/publish ./
# Proves the SkiaSharp native dependency actually works in *this* image, rather
# than assuming a local dev-machine build behaves the same in a slim Linux
# runtime — runs as the same non-root user the container starts as.
USER nimbus
RUN dotnet Nimbus.API.dll --render-smoke-test
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
#
# `ConnectionStrings__Database` here is a placeholder, never a real secret, and scoped to
# just this command (not a persistent ENV): building the bundle only needs to enumerate
# migrations through AppDbContextFactory (Nimbus.Infrastructure/Persistence/
# AppDbContextFactory.cs), which throws unless *some* connection string is present — it
# never actually opens a connection at build time. The real connection string is supplied
# at runtime, when the bundle executable itself is run (see this file's `migrator`
# ENTRYPOINT below, which passes `--connection "$ConnectionStrings__Database"`).
RUN ConnectionStrings__Database="Server=.;Database=DesignTime;User Id=sa;Password=DesignTime123!;TrustServerCertificate=True;" \
    dotnet ef migrations bundle \
    --project Nimbus.API/Nimbus.Infrastructure/Nimbus.Infrastructure.csproj \
    --startup-project Nimbus.API/Nimbus.API/Nimbus.API.csproj \
    --configuration Release \
    --self-contained -r linux-x64 \
    --output /app/efbundle

FROM mcr.microsoft.com/dotnet/runtime-deps:10.0 AS migrator
WORKDIR /app
RUN useradd --no-create-home --uid 10001 nimbus
# The efbundle is a self-contained single-file executable: at startup it extracts its
# embedded assemblies to a cache directory, which it locates via $HOME by default. This
# container's `nimbus` user has no home directory (--no-create-home), so without this,
# extraction fails with "Default extraction directory [/home/nimbus] either doesn't
# exist or is not accessible" and the migrator exits before ever touching the database.
# /tmp is writable by any user (sticky bit) regardless of $HOME, so point extraction there.
ENV DOTNET_BUNDLE_EXTRACT_BASE_DIR=/tmp
USER nimbus
COPY --from=migrator-build --chown=nimbus:nimbus /app/efbundle ./efbundle
# `ConnectionStrings__Database` is the same config key the API binds
# (Nimbus.Infrastructure/DependencyInjection.cs), so both images read the
# connection string from the same environment variable — just different
# credentials (see infra/db/sqlserver-init.sql: nimbus_migrator vs nimbus_app).
# Exec-form ENTRYPOINT does not expand env vars, hence the shell wrapper.
ENTRYPOINT ["/bin/sh", "-c", "exec ./efbundle --connection \"$ConnectionStrings__Database\""]
