#!/usr/bin/env python3
"""
nimbus-vps-pivot.py  (v3 — fully self-hosted)

Repoints the Nimbus backlog from Azure PaaS to Docker on a self-managed Contabo
VPS, with no Azure services remaining.

  Dry run (default -- prints the plan, changes nothing):
      python3 nimbus-vps-pivot.py

  Apply:
      python3 nimbus-vps-pivot.py --apply

Requires: gh CLI, authenticated (`gh auth status`).

THE TARGET ARCHITECTURE THIS ENCODES
  Compute        Docker Compose on one Contabo VPS, images built in CI, pulled by the host
  Ingress        Caddy, automatic TLS
  Database       SQL Server in a container, Express edition for production licensing
  Migrations     Dedicated migrator container (EF Core bundle), runs to completion
  Object store   Self-hosted MinIO, same container definition in dev and production
  Observability  Grafana / Prometheus / Loki -- three containers, plus node-exporter.
                 The API pushes logs straight to Loki through a Serilog sink, so there
                 is no shipper container. This is the pattern already proven in the
                 FosterFlow compose file rather than a generic reference architecture.
  Backups        restic to an off-site target, client-side encrypted
  Email          External provider (Contabo IPs are blocklisted)

WHAT THIS COSTS YOU, STATED HONESTLY
  Self-hosting the object store moves durability, soft delete and versioning from a
  managed service onto the backup job. That is why the backup issue is P0 with a
  rehearsed restore as a hard acceptance criterion, and why it now covers object
  data as well as the database. Nothing else is keeping a copy of pilot media.
"""

import argparse
import re
import subprocess
import sys

REPO = "NickThys3012/Nimbus"


# ---------------------------------------------------------------------------
# 1. Full rewrites
# ---------------------------------------------------------------------------

REWRITES = {
    # ---- #2 --------------------------------------------------------------
    2: (
        "Set up SQL Server persistence with EF Core and migrations",
        """\
**As a** developer
**I want** an EF Core `AppDbContext` on SQL Server with a controlled migration path
**so that** the schema is always in step with the code.

**Acceptance criteria**
- [ ] `AppDbContext` extends `IdentityDbContext<ApplicationUser>` and is registered with the SQL Server provider.
- [ ] Production runs `mcr.microsoft.com/mssql/server` with `MSSQL_PID` set explicitly — see the licensing note below.
- [ ] The connection string comes from configuration only: `.env` on the server, user-secrets in development. Never committed, never baked into an image.
- [ ] The application does not connect as `sa`. A least-privilege login owns the database, with DDL rights only for as long as migrations run at startup.
- [ ] Migrations are applied by a dedicated `migrator` service built from the same Dockerfile — an EF Core migration bundle that runs to completion and exits. The API declares `depends_on: migrator: condition: service_completed_successfully`, so it can never start against an un-migrated schema.
- [ ] Schema management stays out of application startup code entirely. The API carries no migration responsibility, and therefore no single-instance assumption that would need unpicking later.
- [ ] A failed migration fails the deploy and leaves the previous API container running, rather than starting an API against a half-applied schema.
- [ ] An initial migration exists and applies cleanly to an empty database.
- [ ] `EnableRetryOnFailure` is configured — the database container restarting during a deploy is the realistic transient fault.
- [ ] The API waits for a healthy database rather than crash-looping while SQL Server starts. SQL Server's cold start is slow enough that this genuinely matters on a deploy.
- [ ] The data directory is a named volume with ownership correct for the non-root `mssql` user — a permissions mismatch here is the usual first-run failure.
- [ ] Integration tests run against a real SQL Server instance via Testcontainers, not an in-memory provider.

**On the migrator container**
This supersedes the original "migrate on startup" plan. The pattern is taken from the FosterFlow
compose file, where a `migrator` service built from a separate Dockerfile target applies migrations
and exits before the API is allowed to start. It is strictly better than migrating in `Program.cs`:
schema changes become an explicit, observable deploy step with its own exit code, and nothing
depends on there happening to be exactly one API instance.

**Licensing — resolve before the first production deploy**
Developer Edition is free and is what most Docker examples use, but it is licensed for development
and test only; running it in production is a licence violation. Express Edition is free for
production, with limits worth knowing now rather than later: **10 GB per database**, roughly
**1.4 GB buffer pool**, and a cap of the lesser of 1 socket or 4 cores.

With binary content in the object store rather than database columns, 10 GB of relational rows is a
long runway for this application — but that should be a recorded decision with a monitored headroom
alert, not a surprise years in. Standard Edition licensing is not proportionate to this project.""",
    ),
    # ---- #3 --------------------------------------------------------------
    3: (
        None,
        """\
**As a** developer joining the project
**I want** to run `docker compose up --build` and get the API, the Angular app, a SQL Server and an object store
**so that** onboarding takes minutes rather than an afternoon.

**Acceptance criteria**
- [ ] Compose starts the ASP.NET Core app, a SQL Server container, the `migrator` service and a MinIO container.
- [ ] The MinIO service definition is the same one used in production — no emulator in development and a different product in production, so no behaviour can appear only in production.
- [ ] SQL Server has a `healthcheck` with a `start_period` generous enough for its cold start, and the API and migrator both gate on it. Carried over from FosterFlow, where this already works.
- [ ] Publishing SQL Server's 1433 to the host stays optional and commented out by default, so a locally installed SQL Server does not clash. Also carried over.
- [ ] The app is reachable on a documented port with the Angular frontend served from it.
- [ ] Database and object-store volumes persist across restarts; a documented command resets them.
- [ ] Secrets and seed values come from a committed `.env.example` copied to a git-ignored `.env`.
- [ ] The Dockerfile is multi-stage — a Node stage builds the Angular bundle, an SDK stage builds .NET, and the runtime image contains neither toolchain.
- [ ] Buckets and their policies are created on first start by an idempotent init step, so a fresh clone needs no console clicking.
- [ ] A Mailpit container captures outbound email locally, so nothing is ever sent from a developer machine.
- [ ] `README.md` documents the command and the prerequisites, and a fresh clone was verified against it.

**Notes — Apple Silicon needs a timeboxed check**
The original plan named Azure SQL Edge specifically for ARM support, but that product has been
retired, so that justification no longer holds. Confirm the current story before assuming a Mac
works: the options are running the x64 SQL Server image under emulation (works, noticeably slower),
checking whether a current SQL Server release ships an ARM64 image, or pointing Mac developers at a
shared development instance. MinIO itself is fine on ARM. Worth twenty minutes now rather than
discovering it on someone's first day.""",
    ),
    # ---- #5 --------------------------------------------------------------
    5: (
        "Provision and harden the Contabo VPS as the Docker host",
        """\
**As a** maintainer
**I want** the server's baseline configuration written down and reproducible
**so that** it can be rebuilt from scratch without remembering what was clicked.

**Acceptance criteria**
- [ ] Ubuntu 24.04 LTS installed from the Contabo panel.
- [ ] A non-root `deploy` user in the `docker` and `sudo` groups; root login and password authentication both disabled in `sshd_config`.
- [ ] `ufw` permits only 22, 80 and 443, and is enabled. `fail2ban` is installed and active on SSH.
- [ ] **The `ufw` rules are verified against the running stack from outside the host** — an external port scan, not `ufw status`. Docker writes its own iptables rules that are traversed before ufw's, so any `ports:` mapping in compose is reachable from the internet even when ufw is configured to block that port. This is the single most likely way this deployment ends up accidentally exposed.
- [ ] Docker Engine and the Compose plugin installed from Docker's official repository, not the distribution snap.
- [ ] A swap file exists, sized so that a SkiaSharp render or QuestPDF export spike cannot OOM-kill the API alongside the resident footprints of SQL Server, MinIO and the observability stack.
- [ ] Disk layout is deliberate: the SQL Server data volume, the MinIO data volume and the observability volumes have known locations and their growth is monitored independently of the root filesystem.
- [ ] Unattended security upgrades are enabled.
- [ ] The whole baseline is captured as a committed, idempotent script or Ansible playbook under `infra/` — running it twice changes nothing.
- [ ] `/opt/nimbus/` holds the production compose file, the `Caddyfile` and `.env`; ownership and permissions are documented (`.env` is mode `600`, owned by `deploy`).
- [ ] Rebuilding the host from the panel and re-running the playbook was exercised once, and the elapsed time recorded — that number, plus restore time, is the real recovery objective.

**Notes**
Replaces the original Bicep / App Service / Storage-account scope. With nothing left in Azure there
is no cloud control plane to describe, but the server baseline needs to be code rather than
folklore — it is now the only description of the production environment that exists.""",
    ),
    # ---- #6 --------------------------------------------------------------
    6: (
        "CD pipeline: publish to GHCR and deploy to the VPS on merge to main",
        """\
**As a** maintainer
**I want** a merge to `main` to deploy automatically
**so that** what is on `main` is what is live.

**Acceptance criteria**
- [ ] Deployment runs only after build, unit tests and the end-to-end suite pass.
- [ ] The image is built in the GitHub-hosted runner — never on the VPS — and pushed to GHCR tagged both `latest` and the commit SHA.
- [ ] Layer caching (`type=gha`) keeps a typical build under a few minutes.
- [ ] The deploy step connects over SSH with a dedicated deploy keypair held in `VPS_SSH_KEY`, then runs `docker compose pull` and `up -d` against a SHA-pinned `IMAGE_TAG`.
- [ ] The server authenticates to GHCR with a `read:packages` token stored once in `~/.docker/config.json`; the workflow itself never handles that token.
- [ ] Deployment is gated on `/health/ready` returning healthy after the swap; a failed check fails the workflow.
- [ ] The deploy sequence is explicit: pull the new image, run the `migrator` to completion, then replace the API container. A non-zero exit from the migrator aborts the deploy with the previous API still serving traffic.
- [ ] Rolling back is changing `IMAGE_TAG` to the previous SHA and re-running `up -d` — documented, and exercised once for real.
- [ ] **A rollback of the image does not roll back the database.** Migrations are therefore additive and backward-compatible with the immediately previous release: a column is added in one release and only dropped in a later one, never both together. This constraint is written into `docs/architecture.md`, because a rollback that leaves old code facing a newer schema is not actually a rollback.
- [ ] Only the API container is replaced on a routine deploy. SQL Server, MinIO and the observability services are not restarted, so a release never risks the stateful services.
- [ ] Unused images are pruned on the server after each deploy, so the disk does not silently fill.
- [ ] The `production` GitHub environment has a required reviewer, so an accidental merge cannot deploy unattended.
- [ ] The brief interruption during container swap is measured and stated in the docs. This is not a zero-downtime deploy and should not pretend to be.

**Notes**
Replaces the Azure App Service / OIDC deployment. Build-in-CI, pull-on-server means a broken build
can never take the running site down.""",
    ),
    # ---- #8 --------------------------------------------------------------
    8: (
        "Walking skeleton: Angular page to API to SQL Server, deployed to the VPS",
        """\
**As a** team
**I want** one trivial screen that goes from the Angular app through the API to the database and back, running on the VPS
**so that** every architectural seam is proven before feature work starts.

**Acceptance criteria**
- [ ] `/health/ready` reports application, database and object-store connectivity.
- [ ] An Angular route calls a typed API client and renders a value read from SQL Server.
- [ ] A file is uploaded to and read back from MinIO through the same path, including one download served by a presigned URL rather than through the API.
- [ ] It is reachable at the public hostname over HTTPS, with Caddy terminating TLS and the Angular bundle served same-origin by the API host.
- [ ] The full path was exercised by a merge to `main` — not a manual deployment, and not a manual `docker` command on the server.

**Notes**
This is the Sprint 0 exit criterion. Nothing else in Sprint 0 is done until this is.""",
    ),
    # ---- #11 -------------------------------------------------------------
    11: (
        "Object storage abstraction over a self-hosted S3-compatible store",
        """\
**As a** developer
**I want** a single storage abstraction over an S3-compatible object store
**so that** every feature handling binary content does it the same way, and the store can be replaced without touching feature code.

**Acceptance criteria**
- [ ] A Domain interface exposes upload, download, delete and existence checks, with one Infrastructure implementation over the S3 API (`AWSSDK.S3` or the `Minio` client).
- [ ] Buckets are configured, not hard-coded: `flight-images`, `flight-tracks`, `flight-exports`, `map-cache`.
- [ ] Endpoint, credentials, region and path-style addressing are all configuration, so the identical implementation runs in development and production — and against a hosted S3 provider later, should self-hosting stop being the right call.
- [ ] A documented object-key convention scopes every object to its owner and flight, so an orphan is identifiable.
- [ ] Buckets are private. Downloads are served either through the API or via short-lived presigned URLs — never a permanent public URL.
- [ ] Presigned URL lifetime is configuration, not a literal, and is short.
- [ ] Content type and content length are set on upload, so a browser renders an image inline rather than downloading it.
- [ ] Transient failures retry with backoff; a storage outage surfaces as a handled error, not an unhandled exception.
- [ ] Integration tests run against a real MinIO container, not a mock.

**Two things to verify before building on this**
- **Versioning in the deployed topology.** #78 relies on bucket versioning to make an accidental
  deletion recoverable. MinIO's support for versioning in single-node deployments has varied across
  releases. Confirm it works in the exact topology being deployed before writing #78 against it — if
  it does not, that recoverability has to come from the backup job instead and #78 needs rewording.
- **Licence.** MinIO is AGPLv3. Running it unmodified as a backing service for your own application
  is ordinary practice, but be aware of it rather than surprised by it. Garage is a lighter
  S3-compatible alternative built for small self-hosted deployments if MinIO turns out to be more
  weight than it is worth.

**Alternative considered and rejected**
A plain Docker volume behind this same interface — no S3 container, no credentials, one less thing
to back up. Rejected because it gives up presigned URLs, lifecycle rules and versioning, which #58,
#64 and #78 all lean on, and because it turns a future move to a hosted store into a code change
rather than a config change. Worth revisiting if the MinIO container proves to be disproportionate
operational weight for a single-pilot workload.""",
    ),
    # ---- #12 -------------------------------------------------------------
    12: (
        "Structured logging, metrics and traces on a self-hosted Grafana stack",
        """\
**As a** maintainer
**I want** logs, metrics and traces from both the server and the browser in one place I can query
**so that** I can diagnose a problem a pilot reports without reproducing it.

**Acceptance criteria**
- [ ] Three services in the production compose stack: **Grafana, Prometheus and Loki**. Plus `node-exporter` for host CPU, memory and disk. No log shipper — see the note below.
- [ ] The API pushes structured logs directly to Loki through a Serilog sink, configured by a `Loki__Url` pointing at the internal service address.
- [ ] Serilog **also** writes structured JSON to stdout, so `docker logs` remains useful when Loki is unreachable or is itself the thing that is broken. Losing the log destination must not mean losing the logs.
- [ ] Prometheus scrapes the API's `/metrics` endpoint and `node-exporter`. Nothing pushes metrics; there is no agent in this path.
- [ ] **Only Grafana is reachable from outside, through an authenticated Caddy route. Prometheus, Loki and node-exporter declare no `ports:` at all.** See the deliberately-stated trap in the compose issue: a published port is reachable from the internet regardless of `ufw`, so an exposed Loki would accept writes from anyone and an exposed Grafana would be a login prompt on the public internet.
- [ ] The Grafana admin password comes from the server `.env` with **no default fallback value**. A `${GRAFANA_ADMIN_PASSWORD:-admin}` style default is acceptable in local development and unacceptable on a public host.
- [ ] **Retention is set explicitly on both Prometheus (`--storage.tsdb.retention.time`) and Loki, and Docker log rotation is configured.** Unbounded observability filling the disk is the specific failure mode to design against — the monitoring stack taking down the thing it was monitoring.
- [ ] Memory limits are set on each of these containers and the total is reflected in the host RAM budget.
- [ ] Dashboards and alert rules are provisioned from committed configuration, not clicked together by hand, so they survive a host rebuild.
- [ ] Alert rules cover: API unavailable, error-rate spike, root-filesystem headroom, MinIO volume headroom, certificate expiry, container restart loops, database size approaching the Express ceiling, and backup job staleness.
- [ ] The Angular app reports page views and unhandled errors to an API endpoint that logs them into the same pipeline.
- [ ] No personal data, passwords, tokens or presigned URLs are ever logged; covered by a test or an analyser rule.

**Notes — three containers, not six**
An earlier draft of this issue specified a six-container stack with Grafana Alloy as a collection
agent. That was a generic reference architecture, not what this project needs. The FosterFlow compose
file already proves a simpler arrangement that works: the API is its own log shipper via the Serilog
Loki sink, and Prometheus pulls metrics, so neither path needs an agent.

**What the direct sink does not cover.** It carries the API's logs and nothing else. SQL Server,
MinIO, Caddy and Prometheus logs stay in `docker logs` only. In development that is irrelevant; on
the VPS it matters, because "why did that deploy fail" is often answered by a Caddy certificate error
or SQL Server refusing to start — neither of which will appear in Grafana. Adding a shipper
(Promtail, or Grafana Alloy which supersedes it) to collect container logs is a reasonable **later**
addition when that gap bites. It is not a Sprint 0 component, and it should not be treated as one.

**Deliberately deferred, each with a trigger rather than a date:**
- `cadvisor` — per-container CPU and memory attribution. Add it when node-exporter says the host is
  under memory pressure and you cannot tell which container is responsible.
- **Tempo and OpenTelemetry tracing** — add when a problem spans enough components that logs alone
  cannot reconstruct the sequence. With one API service, traces buy far less than they do across a
  microservice mesh.

**Self-hosted monitoring cannot tell you the host is down**, because it goes down with it. This
stack is for diagnosis, not availability alerting — the external uptime check is a separate issue
and is not optional.""",
    ),
    # ---- #75 ------------------------------------------------------------
    75: (
        None,
        """\
**As a** maintainer
**I want** the critical journeys checked in a real browser before deployment
**so that** a regression is caught before a pilot finds it.

**Acceptance criteria**
- [ ] Covered journeys: register and approve, log in, set up reference data, create and save a dossier, run a simulation, export a PDF, share a flight, mark as flown.
- [ ] The suite runs against the containerised stack — API, SQL Server and MinIO — brought up inside the GitHub-hosted runner with a seeded database, deterministically.
- [ ] The suite never runs against the production host or its data.
- [ ] External providers are stubbed so tests do not depend on third-party availability.
- [ ] The suite runs before the deploy job and can be skipped by an explicit commit marker for documentation-only changes.
- [ ] Failures publish a trace, screenshot and video as workflow artifacts.
- [ ] Total runtime stays under ten minutes — including SQL Server's cold start, which is the part most likely to blow the budget.""",
    ),
}


# ---------------------------------------------------------------------------
# 2. Surgical patches
# ---------------------------------------------------------------------------

PATCHES = {
    77: [
        # Query Store ships with SQL Server 2016+; only the Azure qualifier was wrong.
        ("Azure SQL Query Store is enabled", "Query Store is enabled"),
    ],
    78: [
        (
            "Storage lifecycle rules move old exports to a cool tier.",
            "A bucket lifecycle rule expires old exports on a documented schedule.",
        ),
        (
            "Soft delete is enabled on the storage account so an accidental deletion is recoverable.",
            "Bucket versioning is enabled so an accidental deletion is recoverable — subject to the "
            "topology check in #11. If versioning is unavailable, recoverability comes from the "
            "backup job instead and that is stated here explicitly rather than assumed.",
        ),
    ],
}


# ---------------------------------------------------------------------------
# 3. Vocabulary sweep — Azure blob nouns to S3 nouns.
#    Ordered longest-first. Deliberately does NOT touch the bare word
#    "container", because Docker containers are also containers; anything
#    left over is reported for manual review instead of guessed at.
# ---------------------------------------------------------------------------

VOCAB_TARGETS = [31, 50, 51, 57, 58, 62, 64, 69, 78]

VOCAB = [
    ("short-lived user-delegation SAS URLs", "short-lived presigned URLs"),
    ("user-delegation SAS URLs", "presigned URLs"),
    ("user-delegation SAS URL", "presigned URL"),
    ("SAS URLs", "presigned URLs"),
    ("SAS URL", "presigned URL"),
    ("Azurite", "MinIO"),
    ("the Azure Storage account", "the object store"),
    ("an Azure Storage account", "an object store"),
    ("Azure Storage account", "object store"),
    ("Azure Storage SDK", "S3 client"),
    ("Azure Storage", "the object store"),
    ("blob containers", "buckets"),
    ("blob container", "bucket"),
    ("Blob storage", "Object storage"),
    ("blob storage", "object storage"),
    ("`flight-images` container", "`flight-images` bucket"),
    ("`flight-tracks` container", "`flight-tracks` bucket"),
    ("`flight-exports` container", "`flight-exports` bucket"),
    ("`map-cache` container", "`map-cache` bucket"),
    ("Containers are private", "Buckets are private"),
    ("Containers are configured", "Buckets are configured"),
    ("every container", "every bucket"),
    ("blob reference", "object reference"),
    ("blob naming convention", "object-key convention"),
    ("blobs", "objects"),
    # Articles before the bare noun, before the bare-noun rule below,
    # otherwise "a blob" becomes "a object".
    ("a blob", "an object"),
    ("A blob", "An object"),
    ("blob", "object"),
]

# Flags leftover "container" that probably means bucket, ignoring Docker senses.
CONTAINER_RE = re.compile(r"^.*\bcontainer", re.IGNORECASE)
DOCKER_SENSE = re.compile(r"docker|containeris|containeriz|the container|API container", re.I)

# Run after VOCAB, so article agreement is fixed regardless of which phrase rule
# fired first. Cheaper than ordering every article variant correctly.
ARTICLE_FIXES = [
    (re.compile(r"\ba object\b"), "an object"),
    (re.compile(r"\bA object\b"), "An object"),
    (re.compile(r"\ban bucket\b"), "a bucket"),
    (re.compile(r"\bAn bucket\b"), "A bucket"),
]


def apply_vocab(body):
    """Azure blob nouns -> S3 nouns, then fix up article agreement."""
    out = body
    for old, repl in VOCAB:
        out = out.replace(old, repl)
    for pattern, repl in ARTICLE_FIXES:
        out = pattern.sub(repl, out)
    return out


# ---------------------------------------------------------------------------
# 4. Epic bodies
# ---------------------------------------------------------------------------

EPIC_REWRITES = {
    79: (
        None,
        """\
Everything needed before feature work can start: a layered .NET 10 solution with an Angular
frontend, SQL Server persistence, a self-hosted S3-compatible object store, containerised local
development and an automated path to production on a self-managed Docker host.

**Definition of done for this epic**
- A developer can clone the repo and run the full stack with one command.
- A commit on `main` reaches the production host without manual steps.
- The layering rules and the frontend/backend boundary are documented and enforced.
- No component depends on a managed cloud service.""",
    ),
    90: (
        None,
        """\
All binary content — uploaded meteo and trajectory images, KML tracks, generated PDF dossiers and
rendered map images — lives in a self-hosted S3-compatible object store rather than in the database
or loose on the Docker host's filesystem.

**Definition of done for this epic**
- No binary payload is stored in a database column.
- The same object-store service definition runs in development and production.
- Every object has an owner, a bucket, a key convention and a deletion path.
- Pilot media survives the loss of the host — via the off-site backup, not via the store itself.
  A single-node object store is not durable on its own, and this epic does not pretend otherwise.""",
    ),
    92: (
        None,
        """\
Build, test, release-note, deploy — automated, against a single self-managed Docker host. Images are
built in CI and pulled by the server; the server is never a build machine.

**Definition of done for this epic**
- A merged PR is live within minutes with no human in the loop.
- The server baseline can be recreated from scratch from the repo.
- A rollback, a database restore and an object-store restore have each been performed for real,
  not merely documented.
- The deployment's limitations — no redundancy, a brief interruption per release, recovery bounded
  by restore time — are stated honestly in the docs rather than discovered during an incident.""",
    ),
}


# ---------------------------------------------------------------------------
# 5. New issues
# ---------------------------------------------------------------------------

NEW_ISSUES = [
    {
        "title": "Production Docker Compose stack behind Caddy with automatic TLS",
        "milestone": "Sprint 0 — Foundation",
        "labels": ["type:task", "epic:cicd", "area:infra", "feature"],
        "sprint": "Sprint 0", "points": 5, "priority": "P0",
        "body": """\
**As a** maintainer
**I want** the production stack described in one committed compose file
**so that** the running system matches the repository and can be recreated.

**Acceptance criteria**
- [ ] `docker-compose.prod.yml` defines the API, SQL Server, the `migrator`, MinIO, Caddy, Grafana, Prometheus, Loki and node-exporter, each with `restart: unless-stopped` (the migrator with `restart: "no"`).
- [ ] **Only Caddy publishes ports — 80 and 443. Every other service has no `ports:` key whatsoever.** This is not defence in depth behind the firewall: Docker's iptables rules are traversed before ufw's, so a published port is reachable from the internet even with ufw configured to block it. A published Loki accepts log writes from anyone; a published Grafana is a login prompt on the open internet.
- [ ] Where a port is genuinely needed for debugging, it is bound as `127.0.0.1:PORT:PORT` so it is reachable only through an SSH tunnel — never a bare `PORT:PORT`.
- [ ] The dev compose file's published ports for SQL Server, Grafana, Prometheus and Loki are absent here, and the difference between the two files is called out in a comment so the omission reads as deliberate rather than forgotten.
- [ ] The API image reference is `ghcr.io/nickthys3012/nimbus:${IMAGE_TAG}`, never a locally built image — no `build:` key in the production file.
- [ ] A committed `Caddyfile` reverse-proxies the hostname to the API and exposes Grafana and the MinIO console on separate authenticated routes.
- [ ] Every stateful service uses a named volume: SQL Server data, MinIO data, Caddy `/data`, Loki and Prometheus. `docker compose down` destroys none of them.
- [ ] The API declares `depends_on` both the database and MinIO with `service_healthy` conditions.
- [ ] Security headers — HSTS, `X-Content-Type-Options`, a referrer policy — are set in the Caddyfile.
- [ ] Bringing the stack up on a blank server with only the compose file, the Caddyfile and a populated `.env` was verified once, end to end.""",
    },
    {
        "title": "Self-hosted MinIO object store in the production stack",
        "milestone": "Sprint 0 — Foundation",
        "labels": ["type:task", "epic:storage", "area:infra", "feature"],
        "sprint": "Sprint 0", "points": 3, "priority": "P0",
        "body": """\
**As a** maintainer
**I want** the object store running as a first-class service on the host
**so that** binary content has a private, addressable home that is not the application container.

**Acceptance criteria**
- [ ] MinIO runs as a compose service with a named data volume, publishing no ports — reachable on the internal network, with the console behind an authenticated Caddy route.
- [ ] Root credentials are generated rather than defaulted, live in the server `.env`, and are **not** the credentials the application uses.
- [ ] The application authenticates with a dedicated access key, scoped by policy to only the four buckets it needs, with no admin rights.
- [ ] Buckets and policies are created by an idempotent init step, so a rebuild needs no console clicking.
- [ ] Anonymous access is verified impossible by an explicit test, not assumed from the default configuration.
- [ ] Bucket versioning is enabled if the deployed topology supports it — see the check in #11 — and the outcome is recorded either way.
- [ ] A memory limit is set and MinIO appears in the host RAM budget.
- [ ] The data volume's disk headroom is alerted on separately from the root filesystem.
- [ ] Restoring the data volume from backup into an empty MinIO instance has been performed once, and the objects verified readable through the application.

**Notes**
This service is now the only copy of pilot media on the host, which makes it the reason the backup
issue is P0. Treat it as stateful infrastructure, not a cache.""",
    },
    {
        "title": "Multi-stage production image: Angular bundle inside the .NET runtime",
        "milestone": "Sprint 0 — Foundation",
        "labels": ["type:task", "epic:foundation", "area:infra", "feature"],
        "sprint": "Sprint 0", "points": 3, "priority": "P0",
        "body": """\
**As a** maintainer
**I want** one image containing the API and the compiled Angular bundle
**so that** frontend and backend can never drift apart in production.

**Acceptance criteria**
- [ ] A single `Dockerfile`: a Node stage running `npm ci` and the production Angular build, an SDK stage running `dotnet publish`, and a runtime stage on the ASP.NET Core 10 runtime image.
- [ ] The Angular output is copied into the API's `wwwroot` during publish, so the SPA is served same-origin.
- [ ] Neither the Node toolchain nor the .NET SDK is present in the final image.
- [ ] The final image runs as a non-root user.
- [ ] The native dependencies SkiaSharp needs at runtime are present — a map render is exercised inside the built image, not assumed to work. This is the most common way a working local build fails in a slim runtime image.
- [ ] `.dockerignore` excludes `node_modules`, `bin`, `obj` and `.git`, keeping the build context small.
- [ ] Layer ordering puts `npm ci` and `dotnet restore` ahead of source copies, so a source-only change does not re-resolve dependencies.
- [ ] The release-notes JSON is baked in at build time, keeping the in-app changelog honest about what is running.
- [ ] The resulting image size is recorded in the PR description as a baseline to watch.""",
    },
    {
        "title": "Health and readiness endpoints the deploy gate and monitor can rely on",
        "milestone": "Sprint 0 — Foundation",
        "labels": ["type:task", "epic:foundation", "area:backend", "feature"],
        "sprint": "Sprint 0", "points": 2, "priority": "P0",
        "body": """\
**As a** maintainer
**I want** the API to report whether it is actually able to serve
**so that** the deploy gate, the compose healthcheck and the uptime monitor all read the same truth.

**Acceptance criteria**
- [ ] `/health/live` answers as soon as the process is up, performing no dependency checks.
- [ ] `/health/ready` checks SQL Server and MinIO, and fails when either is unreachable.
- [ ] Both are anonymous but leak nothing — no version string, no connection details, no infrastructure topology.
- [ ] The compose healthcheck uses `/health/live`; the CD gate and the external monitor use `/health/ready`.
- [ ] The checks are cheap enough to poll every few seconds without measurable load.
- [ ] Stopping the database container, and separately the MinIO container, each make `/health/ready` fail — verified by hand once, not assumed.""",
    },
    {
        "title": "Configuration and secrets inventory for the VPS deployment",
        "milestone": "Sprint 0 — Foundation",
        "labels": ["type:task", "epic:cicd", "area:infra", "feature"],
        "sprint": "Sprint 0", "points": 2, "priority": "P0",
        "body": """\
**As a** maintainer
**I want** every configuration value and secret catalogued with its source
**so that** a rebuild is not an archaeology exercise.

**Acceptance criteria**
- [ ] `docs/configuration.md` lists every setting: name, purpose, where it lives (GitHub secret, server `.env`, committed default) and whether it is sensitive.
- [ ] The GitHub secrets in use are exactly `VPS_HOST`, `VPS_SSH_KEY` and the email provider key — each documented with its rotation procedure.
- [ ] The server `.env` lives at `/opt/nimbus/.env`, mode `600`, owned by `deploy`, with a committed `.env.example` counterpart holding placeholder values.
- [ ] Every secret is inventoried with an owner and a rotation date: the SQL Server login, the MinIO root credential, the MinIO application access key, the Grafana admin password, the restic repository password, and the email provider key.
- [ ] **No secret in the production compose file or `.env` has a default fallback value.** Development conveniences like `${GRAFANA_ADMIN_PASSWORD:-admin}` or `MSSQL_PID: "Developer"` must not survive into the production file — a missing variable should stop the stack starting, not silently substitute a guessable default. Verified by starting the stack with an empty `.env` and confirming it refuses.
- [ ] **The restic repository password is stored somewhere other than the server it protects.** A backup encryption key that only exists on the machine being backed up makes the backups unrecoverable in exactly the scenario they are for.
- [ ] No secret is ever baked into an image, echoed in a workflow log, or committed. A secret-scanning check runs in CI.
- [ ] The deploy keypair is dedicated to deployment rather than a reused personal key.
- [ ] The GHCR `read:packages` token has its expiry recorded, with a reminder set before it lapses — an expired pull token is a silent deploy failure.""",
    },
    {
        "title": "Nightly backup of database and object store, off-site and encrypted, with a rehearsed restore",
        "milestone": "Sprint 0 — Foundation",
        "labels": ["type:task", "epic:cicd", "area:infra", "feature"],
        "sprint": "Sprint 0", "points": 5, "priority": "P0",
        "body": """\
**As a** pilot
**I want** my flight history and photographs to survive the server being lost
**so that** years of logbook data are not one bad command away from gone.

**Acceptance criteria**
- [ ] A nightly job runs `BACKUP DATABASE` to a compressed `.bak` with `CHECKSUM`, and `RESTORE VERIFYONLY` runs against it immediately, so a corrupt file is caught the same night rather than at recovery time.
- [ ] Object data is backed up as well, by `mc mirror` of the buckets or by snapshotting the MinIO data volume. Self-hosting means nothing else holds a copy of pilot media.
- [ ] Both are pushed off the host with `restic`, which encrypts client-side, so the off-site target only ever holds ciphertext.
- [ ] The off-site target has a failure domain independent of the VPS — a machine at home, a second provider, or a dumb storage box. **RAID inside Contabo is not an off-site copy, and a copy on the box being backed up is not a backup.**
- [ ] Retention is defined and enforced (for example seven daily, four weekly, twelve monthly), and `restic forget --prune` runs on that schedule.
- [ ] The job's success or failure is a metric in Prometheus with an alert on staleness — a backup that silently stopped running two months ago is the failure mode to design against.
- [ ] **A full restore — database and objects — has been performed into a scratch environment and verified by opening a restored dossier through the application.** This criterion is not met by documentation alone.
- [ ] Restore duration and the exact commands are recorded in the runbook. That duration is the recovery time objective, whether or not anyone calls it that.

**On the two Contabo snapshots**
The plan includes two snapshots. Use them — but not as any part of this issue.

A snapshot is good for exactly one thing: rolling the host back before a risky OS upgrade or Docker
version bump. It is not a backup, for two reasons. It lives in the same account with the same
provider, so it shares a failure domain with the thing it is meant to protect. And a snapshot of a
running SQL Server is crash-consistent rather than transactionally consistent — SQL Server can
usually recover from that, but "usually" is not a property worth having in a recovery path.

Take a snapshot before maintenance. Rely on restic for everything else.

**Notes**
Raised from 3 to 5 points, and this is the issue that going fully self-hosted really pays for. A
managed storage account was previously providing durability, soft delete and versioning at no
effort; all of it now lives here.

On the tension between "fully self-hosted" and "off-site": an off-site copy by definition requires
somewhere that is not this server. Client-side encryption via restic is what reconciles those — the
target holds only ciphertext, so a rented storage box or a machine at home both work without
anyone else being able to read pilot data.""",
    },
    {
        "title": "External uptime and certificate monitoring, independent of the host",
        "milestone": "Sprint 0 — Foundation",
        "labels": ["type:task", "epic:cicd", "area:infra", "feature"],
        "sprint": "Sprint 0", "points": 2, "priority": "P1",
        "body": """\
**As a** maintainer
**I want** to hear that the app is down from something that is not the app
**so that** an outage at 05:00 before a dawn flight is not discovered by the person trying to fly.

**Acceptance criteria**
- [ ] An off-host uptime check polls `/health/ready` and alerts on failure. Off-host by necessity: the Grafana stack runs on the machine being monitored and goes down with it.
- [ ] Certificate expiry is checked externally, so a silent Caddy renewal failure is caught before the site starts warning users.
- [ ] Alerts arrive in a channel that is actually read outside working hours.
- [ ] The alert threshold tolerates the brief interruption of a routine deploy without paging.
- [ ] Every alert has a documented first response in the runbook.

**Notes**
Deliberately separate from the Grafana stack in #12: that is for diagnosis, this is for
availability, and the two cannot share a failure domain. With everything now self-hosted there is no
provider watching this host, and a dawn balloon flight is prepared the evening before with no time
to debug at first light.""",
    },
    {
        "title": "Transactional email through an external provider",
        "milestone": "Sprint 1 — Accounts & Admin",
        "labels": ["type:task", "epic:auth", "area:backend", "feature"],
        "sprint": "Sprint 1", "points": 3, "priority": "P0",
        "body": """\
**As a** prospective user
**I want** the registration and approval emails to actually arrive
**so that** I can get into the app at all.

**Acceptance criteria**
- [ ] Email is sent through an external provider's API or authenticated SMTP relay (Resend, Postmark or Brevo) — never by an MTA running on the VPS.
- [ ] SPF, DKIM and DMARC records are configured for the sending domain and confirmed against a deliverability checker.
- [ ] Templates exist for registration received, account approved, and account rejected or deactivated.
- [ ] In development, mail is captured by the local Mailpit container rather than sent.
- [ ] A send failure is logged and surfaced to an admin, and never rolls back or blocks the registration transaction itself.
- [ ] The provider API key lives only in the server `.env`.

**Notes**
The one place where self-hosting is not viable, so worth being explicit about why. Contabo IP ranges
appear on spam blocklists widely enough that mail sent from the VPS is silently dropped by most
receivers — and the failure looks like success from the sending side. Running your own MTA also means
owning deliverability reputation, which is a considerably larger job than it appears. This blocks the
approval flow in #14 and #19.""",
    },
    {
        "title": "DNS, reverse DNS and the production hostname",
        "milestone": "Sprint 0 — Foundation",
        "labels": ["type:task", "epic:cicd", "area:infra", "feature", "good first issue"],
        "sprint": "Sprint 0", "points": 1, "priority": "P1",
        "body": """\
**As a** pilot
**I want** to reach the app at a real hostname over HTTPS
**so that** I am not typing an IP address and clicking past a certificate warning.

**Acceptance criteria**
- [ ] An A record — and an AAAA record if IPv6 is in use — points the production hostname at the VPS.
- [ ] Reverse DNS for the VPS IP is set in the Contabo panel to match the hostname.
- [ ] Caddy holds a valid certificate for the hostname, and the renewal path has been confirmed rather than assumed.
- [ ] HTTP redirects to HTTPS.
- [ ] Hostname, registrar, DNS provider and domain renewal date are recorded in `docs/configuration.md`.

**Notes**
Must land before the walking skeleton in #8 — Caddy cannot obtain a certificate until DNS resolves
to the host.""",
    },
    {
        "title": "RAM budget, container resource limits and restart policies",
        "milestone": "Sprint 0 — Foundation",
        "labels": ["type:task", "epic:quality", "area:infra", "feature"],
        "sprint": "Sprint 0", "points": 3, "priority": "P1",
        "body": """\
**As a** maintainer
**I want** the host's memory allocated deliberately across every container
**so that** one greedy operation cannot take the whole stack down with it.

**Host: 6 vCPU / 12 GB RAM / 200 GB SSD**

**Acceptance criteria**
- [ ] The memory ceilings below are applied via a `docker-compose.limits.yml` override, committed alongside the production compose file.
- [ ] **The sum of memory limits fits in physical RAM.** Target is ~8.9 GB of ceilings against 12 GB, leaving ~3.1 GB for the kernel, page cache, SSH, cron and restic. This is what makes the limits a real guarantee rather than eight ceilings that can collectively overcommit the box. Adding a service means taking memory from another, not from the headroom.

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

- [ ] SQL Server's memory is set explicitly via `MSSQL_MEMORY_LIMIT_MB: 1792` — comfortably below its 2560M container ceiling, so it backs off before the OOM killer intervenes. Express caps the buffer pool near 1410 MB regardless, but the process footprint sits well above that.
- [ ] CPU limits deliberately sum to more than 6. CPU is a share, not a reservation, so oversubscription is correct: the API can burst into idle cores during a SkiaSharp render while a runaway Prometheus query still cannot starve it.
- [ ] Prometheus is budgeted as the volatile one — its footprint scales with active series and retention, not traffic. Its retention setting and memory limit are decided together, not independently.
- [ ] Native allocations are understood: SkiaSharp and QuestPDF allocate outside the .NET GC heap, so no GC setting governs them and the container limit is the only real guard on the API.
- [ ] Docker log rotation is set daemon-wide in `/etc/docker/daemon.json` (`max-size: 50m`, `max-file: 3`) rather than per service, so it cannot be forgotten on a service added later.
- [ ] Disk allocation is documented and monitored per volume, not just for the root filesystem: SQL Server data ~25 GB, MinIO ~100 GB, Loki ~20 GB, Prometheus ~10 GB, OS and images ~20 GB, backup staging ~15 GB, ~10 GB unallocated.
- [ ] A concurrent burst of PDF exports and map renders was load-tested with the full stack running, and the API stayed within its limit instead of being OOM-killed.
- [ ] `restart: unless-stopped` is set on every service, and the stack returns automatically after a host reboot — verified by actually rebooting.
- [ ] A documented upload-size ceiling is enforced at both Caddy and the API, so one large file cannot exhaust the disk.

**Notes**
Steady-state usage will be roughly 3.5–4.5 GB against 12 GB available, so this host is comfortable
rather than tight — the limits exist to contain spikes and mistakes, not to ration a scarce resource.
That is a better position than earlier drafts of this plan assumed, and it means the observability
stack can stay on-box without argument.

The one genuinely variable number is MinIO's disk. 200 GB is generous for a single-pilot logbook, but
media accumulates and nothing prunes it automatically — which is what #78's orphan cleanup is for.""",
    },
    {
        "title": "Operational runbook for the single-host deployment",
        "milestone": "Backlog",
        "labels": ["type:task", "epic:cicd", "area:infra", "feature"],
        "sprint": "Backlog", "points": 3, "priority": "P2",
        "body": """\
**As a** maintainer returning to this after three months away
**I want** the routine operations written down
**so that** I am not rediscovering them under pressure.

**Acceptance criteria**
- [ ] `docs/runbook.md` covers: deploying, rolling back, restarting a single service, reading logs, restoring the database, restoring the object store, renewing or debugging a certificate, recovering from a full disk, responding to the Express size ceiling, and rotating each secret.
- [ ] Every procedure is written as commands to run, not prose describing intent.
- [ ] Each procedure has been executed at least once by whoever wrote it.
- [ ] The rebuild-from-nothing path is written end to end: fresh VPS, playbook, compose up, restore from restic, verify. This is the only document that describes how to get the system back.
- [ ] The document states plainly what this deployment does **not** have — no redundancy, no zero-downtime deploy, a brief interruption on every release, recovery bounded by restore time — so future expectations are set honestly rather than discovered during an incident.""",
    },
]


LABEL_EDITS = [
    ("area:infra", "Server, Docker, pipelines, self-hosted services"),
    ("epic:storage", "Object storage & media handling"),
]


# ---------------------------------------------------------------------------
# Plumbing
# ---------------------------------------------------------------------------

class Runner:
    def __init__(self, repo, apply):
        self.repo = repo
        self.apply = apply
        self.warnings = []
        self.review = []

    def gh(self, args, stdin=None):
        cmd = ["gh"] + args
        try:
            r = subprocess.run(cmd, input=stdin, capture_output=True,
                               text=True, check=True)
            return r.stdout
        except FileNotFoundError:
            self.warnings.append("gh CLI not found on PATH.")
            return None
        except subprocess.CalledProcessError as e:
            self.warnings.append(f"{' '.join(cmd[:4])}...: {(e.stderr or '').strip()[:220]}")
            return None

    def fetch_body(self, num):
        out = self.gh(["issue", "view", str(num), "--repo", self.repo,
                       "--json", "body", "-q", ".body"])
        return out if out is None else out.rstrip("\n")

    def edit(self, num, title, body, what):
        if not self.apply:
            t = f'\n       title -> "{title}"' if title else ""
            print(f"  #{num:<3} {what}{t}")
            return
        args = ["issue", "edit", str(num), "--repo", self.repo, "--body-file", "-"]
        if title:
            args += ["--title", title]
        if self.gh(args, stdin=body) is not None:
            print(f"  #{num:<3} {what} — done")

    def create(self, spec):
        if not self.apply:
            print(f"  NEW  {spec['title']}")
            return
        args = ["issue", "create", "--repo", self.repo,
                "--title", spec["title"], "--body-file", "-",
                "--milestone", spec["milestone"]]
        for lb in spec["labels"]:
            args += ["--label", lb]
        out = self.gh(args, stdin=spec["body"])
        if out:
            print(f"  NEW  {spec['title']}\n       {out.strip().splitlines()[-1]}")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--repo", default=REPO)
    ap.add_argument("--apply", action="store_true",
                    help="actually make the changes (default is a dry run)")
    args = ap.parse_args()

    r = Runner(args.repo, args.apply)
    print("\nNimbus backlog: Azure PaaS -> fully self-hosted Docker on VPS")
    print("  Database      SQL Server (Express) + dedicated migrator container")
    print("  Object store  self-hosted MinIO, same definition in dev and prod")
    print("  Telemetry     Grafana / Prometheus / Loki + node-exporter (no shipper:")
    print("                the API pushes to Loki via Serilog, Prometheus pulls)")
    print("  Backups       restic, client-side encrypted, off-site")
    print("  Email         external provider (the one unavoidable exception)")
    print(f"\nRepo: {args.repo}")
    print(f"Mode: {'APPLYING' if args.apply else 'DRY RUN — nothing will change'}\n")

    print("Rewriting issues")
    for num, (title, body) in REWRITES.items():
        r.edit(num, title, body, "rewritten")

    print("\nUpdating epic descriptions")
    for num, (title, body) in EPIC_REWRITES.items():
        r.edit(num, title, body, "epic body updated")

    print("\nPatching individual acceptance criteria")
    for num, pairs in PATCHES.items():
        body = r.fetch_body(num)
        if body is None:
            print(f"  #{num:<3} SKIPPED — could not read issue")
            continue
        new, missed = body, []
        for old, repl in pairs:
            if old in new:
                new = new.replace(old, repl)
            else:
                missed.append(old[:55])
        for m in missed:
            r.warnings.append(f"#{num}: expected text not found -> '{m}...'")
        if new != body:
            r.edit(num, None, new, f"{len(pairs) - len(missed)} line(s) patched")
        else:
            print(f"  #{num:<3} no change needed")

    print("\nVocabulary sweep: Azure blob nouns -> S3 nouns")
    for num in VOCAB_TARGETS:
        body = r.fetch_body(num)
        if body is None:
            print(f"  #{num:<3} SKIPPED — could not read issue")
            continue
        new = apply_vocab(body)
        # Anything still saying "container" in a storage sense needs eyes, not a regex.
        for line in new.splitlines():
            if CONTAINER_RE.match(line) and not DOCKER_SENSE.search(line):
                r.review.append(f"#{num}: {line.strip()[:100]}")
        if new != body:
            r.edit(num, None, new, "vocabulary updated")
        else:
            print(f"  #{num:<3} no change needed")

    print("\nCreating new issues")
    for spec in NEW_ISSUES:
        r.create(spec)

    print("\nUpdating label descriptions")
    for name, desc in LABEL_EDITS:
        if not args.apply:
            print(f'  {name} -> "{desc}"')
        elif r.gh(["label", "edit", name, "--repo", args.repo,
                   "--description", desc]) is not None:
            print(f"  {name} — done")

    if r.review:
        print("\nNeeds your eyes — 'container' left in a possibly-storage sense:")
        for line in r.review:
            print(f"  ? {line}")

    if r.warnings:
        print("\nWarnings")
        for w in dict.fromkeys(r.warnings):
            print(f"  ! {w}")

    print("\nBoard fields for the new issues (set by hand or with `gh project item-edit`):")
    print(f"  {'Sprint':<10} {'Pts':<4} {'Pri':<4} Title")
    s0 = 0
    for s in NEW_ISSUES:
        if s["sprint"] == "Sprint 0":
            s0 += s["points"]
        print(f"  {s['sprint']:<10} {s['points']:<4} {s['priority']:<4} {s['title'][:56]}")
    print(f"\n  Sprint 0 was 46 points across 12 issues; this adds {s0}.")
    print("  That is two or three sprints of work, not one. Suggested split:")
    print("    Sprint 0a  VPS baseline, compose+Caddy, MinIO, Dockerfile, DNS, health, CD")
    print("    Sprint 0b  backups+restore rehearsal, RAM budget, uptime check, Grafana")
    print("  Do not ship to real pilots before 0b. The backup restore is the gate.")

    if not args.apply:
        print("\nDry run complete. Re-run with --apply to make these changes.")
    print()


if __name__ == "__main__":
    sys.exit(main())
