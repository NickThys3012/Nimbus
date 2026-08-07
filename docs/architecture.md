# Nimbus – Architecture

This document describes the current architecture of Nimbus: a .NET API
following Clean Architecture, paired with an Angular single-page app.

## High-level overview

```
┌─────────────────────────┐        HTTP (JSON)        ┌──────────────────────────┐
│      Nimbus.Web          │ ───────────────────────▶ │       Nimbus.API          │
│  Angular 21 (standalone) │ ◀─────────────────────── │  ASP.NET Core (.NET 10)   │
└─────────────────────────┘                            └──────────────────────────┘
                                                                   │
                                                                   ▼
                                                        ┌──────────────────────┐
                                                        │   SQL Server (EF Core)│
                                                        └──────────────────────┘
```

- **Nimbus.Web** — Angular frontend, consumes the API over HTTP.
- **Nimbus.API** — ASP.NET Core Web API, structured as a set of Clean
  Architecture layers (see below), backed by SQL Server via EF Core.
- In **Development**, the API also serves the Angular app's built output
  directly (see [Dev hosting](#dev-hosting-angular--api)), so the two can run
  as a single process if desired — but they are normally run/iterated on
  separately (`ng build --watch` + API from Rider).

## Backend: Nimbus.API solution

The backend is split into layers, each its own project, following a
Clean Architecture / Onion Architecture style with dependencies pointing
inward toward `Nimbus.Domain`:

```
Nimbus.API (host)
 ├─ depends on → Nimbus.Application
 ├─ depends on → Nimbus.Infrastructure
 ├─ depends on → Nimbus.Logging
 └─ depends on → Nimbus.Observability

Nimbus.Infrastructure
 ├─ depends on → Nimbus.Application
 └─ depends on → Nimbus.Domain

Nimbus.Application
 ├─ depends on → Nimbus.Contracts
 └─ depends on → Nimbus.Domain

Nimbus.Contracts
 └─ depends on → Nimbus.Domain

Nimbus.Domain            (no dependencies — innermost layer)
```

### Nimbus.Domain
The innermost layer — plain entities, enums, exceptions, and repository
interfaces. No framework or infrastructure dependencies.

- `Entities/` — e.g. `User` (a domain entity deliberately kept separate from
  ASP.NET Core Identity's user model — see `Infrastructure/Identity`).
- `Entities/Base/BaseEntity` — common entity base.
- `Enums/` — e.g. `UserRole`.
- `Interfaces/` — repository abstractions, e.g. `IUserRepository`.
- `Exceptions/` — domain-specific exception types.

### Nimbus.Contracts
DTOs and mappers shared between `Nimbus.Application` and `Nimbus.API`
(and consumed indirectly by the Angular client via the generated OpenAPI
client — see [Frontend](#frontend-nimbusweb)).

- `DTOs/Features/Auth/` — `LoginRequestDto`, `LoginResponseDto`, `UserDto`.
- `Mappers/AuthMappers.cs` — mapping between domain entities and DTOs.

### Nimbus.Application
Application/business logic layer, using the **CQRS** pattern via **MediatR**,
with **FluentValidation** for request validation.

- `Features/<Feature>/Queries|Commands/<Name>/` — each use case is a folder
  containing its `Query`/`Command`, `Handler`, and `Validator`
  (e.g. `Features/Auth/Queries/GetUserByEmail/`).
- `Common/Behaviours/ValidationBehaviour` — a MediatR pipeline behaviour that
  runs FluentValidation validators automatically before every
  request/handler executes.
- `DependencyInjection.cs` (`AddApplication`) registers MediatR handlers
  (scanned from this assembly) and FluentValidation validators (scanned from
  `Nimbus.Contracts`), plus the validation pipeline behaviour.

### Nimbus.Infrastructure
Implements interfaces defined in `Nimbus.Application`/`Nimbus.Domain` using
concrete technology: EF Core, ASP.NET Core Identity, health checks.

- `Persistence/AppDbContext` — EF Core `DbContext` (SQL Server).
- `Persistence/Repositories/UserRepository` — `IUserRepository` implementation.
- `Persistence/DatabaseMigrator` — applies EF Core migrations at startup.
- `Persistence/IdentitySeed` — seeds initial Identity users/roles.
- `Identity/ApplicationUser`, `Identity/RefreshToken` — ASP.NET Core Identity
  user model and refresh-token storage (kept separate from the `Domain.User`
  entity by design).
- `Identity/TokenService` — issues/validates JWT access tokens and refresh
  tokens.
- `Services/CurrentUserService` — exposes the current authenticated user to
  the application layer.
- `DependencyInjection.cs` (`AddInfrastructure`) wires up EF Core, Identity,
  health checks, and infrastructure services.

### Nimbus.Logging
Centralizes Serilog configuration.

- `DependencyInjection.cs` (`AddLogging`, on `IHostBuilder`) configures
  Serilog: console + rolling file sinks always, plus an optional Grafana
  Loki sink when `Loki:Url` is configured. Enriches logs with machine name,
  thread id, and log context.

### Nimbus.Observability
Application-specific metrics for Prometheus.

- `Services/PrometheusBusinessMetrics` — implements `IBusinessMetrics`
  (defined in `Nimbus.Application`) to record custom business metrics.
- `DependencyInjection.cs` (`AddObservabilityMetrics`) registers the metrics
  service. HTTP-level metrics (`http_requests_received_total`,
  `http_request_duration_seconds`, etc.) are wired separately via
  `prometheus-net`'s `UseHttpMetrics()`/`MapMetrics()` in `Program.cs`.

### Nimbus.API (host)
The ASP.NET Core entry point (`Program.cs`) that composes everything above:

- **Auth**: JWT bearer authentication (`Jwt:*` config), role/claim mapping,
  `[Authorize]`-ready.
- **API docs**: `Microsoft.AspNetCore.OpenApi` (`AddOpenApi`/`MapOpenApi`,
  spec served at `/openapi/v1.json`) with **Scalar** as the interactive
  docs UI (`MapScalarApiReference`, served at `/scalar`).
- **Health checks**: `/health` endpoint (SQL Server + `AppDbContext` checks),
  formatted via `AspNetCore.HealthChecks.UI.Client`.
- **Metrics**: Prometheus scraping endpoint at `/metrics`.
- **Middleware pipeline** (in order): custom `ExceptionHandlingMiddleware` →
  Serilog request logging → HTTPS redirection/HSTS → static files → HTTP
  metrics → authentication → authorization → health checks → OpenAPI/Scalar
  → controllers → metrics → SPA fallback (`index.html`).
- **Startup tasks**: applies EF Core migrations and seeds Identity users on
  boot.
- **Controllers**: thin controllers (`Controllers/AuthenticationController`)
  that delegate to MediatR (`ISender`) for queries/commands, and directly use
  Identity's `UserManager`/`TokenService` for login/refresh/logout (auth
  itself isn't yet routed through the CQRS pipeline).

#### Dev hosting (Angular + API)
In `Development`, `Program.cs` points the API's `WebRootPath` (and
`WebRootFileProvider`) at `Nimbus.Web/dist/Nimbus.Web/browser` if that folder
exists, and falls back unmatched routes to `index.html`
(`MapFallbackToFile`). This lets the API serve the Angular build directly —
run `ng build --watch` in `Nimbus.Web` and hit the API's own port
(`http://localhost:5214`) to see the SPA. In production, the API's `.csproj`
has an MSBuild target (`PublishAngular`) that runs `npm ci` + `ng build` and
copies the output into `wwwroot` as part of `dotnet publish`.

## Frontend: Nimbus.Web

Angular 21, **standalone components** (no `NgModule`s in application code),
built with the modern `@angular/build` (esbuild-based) builder.

- `src/app/Nimbus.ts` / `.html` / `.css` — root component
  (`<Nimbus-root>`), hosts `<router-outlet>`.
- `src/app/Nimbus.config.ts` — the `ApplicationConfig` (`nimbusConfig`)
  bootstrapped in `main.ts`. Registers router, HTTP client, and the
  generated API client's DI requirements (see below).
- `src/app/Nimbus.routes.ts` — route table; feature pages are lazy-loaded
  via `loadComponent`.
- `src/app/pages/<feature>/` — routed, standalone page components
  (e.g. `pages/user-by-mail/`).

### Generated API client
Rather than hand-writing HTTP calls and DTOs, the Angular app generates a
typed client from the API's OpenAPI spec using
[`openapi-typescript-codegen`](https://github.com/ferdikoomen/openapi-typescript-codegen)
(`--client angular`), producing **RxJS Observable–based** services and
models.

- **Location**: `src/app/core/api-client/` — models, services
  (`AuthenticationService`, etc.), and core request plumbing
  (`BaseHttpRequest`, `AngularHttpRequest`, `OpenAPI` config, `ApiError`).
  This directory is generated (marked "do not edit") but **committed to
  git**, not gitignored.
- **Regeneration**: `npm run generate:api` downloads the live spec from the
  running dev API (`http://localhost:5214/openapi/v1.json`) and re-runs the
  generator. The API must be running locally to regenerate. See
  `Nimbus.Web/README.md` for the full workflow.
- **DI wiring**: the generated client's `NgModule` (`NimbusApiClient`) isn't
  imported (the app is standalone), so `Nimbus.config.ts` explicitly
  provides what the generated services need: `provideHttpClient()`, the
  `OpenAPI` config token, and `BaseHttpRequest → AngularHttpRequest`.

### Signals layer over the generated client
Since the generator only produces Observable-based services, a thin
hand-written layer converts them to Angular **signals** for the rest of the
app to consume, without ever editing generated files:

- `src/app/core/auth/auth.store.ts` (`AuthStore`) — example/current pattern:
  - **Mutations** (`login`, `logout`, `refresh`) — one-off actions: call the
    generated Observable method, `.subscribe()`, push results into writable
    `signal()`s (`accessToken`, `email`, `role`, `isLoading`, `error`), with
    `isAuthenticated` as a `computed()`.
  - **Reactive queries** (`userLookup`) — use `rxResource()` from
    `@angular/core/rxjs-interop`, wrapping the generated
    `getApiAuthentication` call. Driven by a `lookupEmail` signal; exposes
    `.value()`, `.isLoading()`, `.error()`, `.hasValue()` as signals that
    automatically refetch when `lookupEmail` changes.

This is the pattern to extend for any future generated service that needs
to be consumed reactively via signals.

## Cross-cutting concerns

- **Validation**: FluentValidation validators live alongside their
  request/query/command in `Nimbus.Application`, and run automatically via
  the MediatR `ValidationBehaviour` pipeline.
- **Logging**: Serilog (console + rolling file, optional Loki sink),
  configured centrally in `Nimbus.Logging` and applied via
  `builder.Host.AddLogging()` in `Program.cs`.
- **Metrics**: Prometheus (`prometheus-net`) for HTTP metrics, plus custom
  business metrics via `IBusinessMetrics`/`PrometheusBusinessMetrics`
  (`Nimbus.Observability`), exposed at `/metrics`.
- **Health checks**: SQL Server + EF Core `DbContext` checks at `/health`.
- **Auth**: JWT bearer tokens (access + refresh), issued/validated by
  `TokenService`, backed by ASP.NET Core Identity (`ApplicationUser`,
  roles). Refresh tokens are set as an HTTP-only, secure, `SameSite=Strict`
  cookie.

## Known gaps / not yet in place

- No CI/CD pipeline or containerization (Dockerfiles/compose) yet.
- Authentication endpoints call `UserManager`/`TokenService` directly from
  the controller rather than going through MediatR commands/queries like
  the rest of the application layer.
- The Angular app's generated API client and signals wrapper currently only
  cover the `Authentication` feature (login/refresh/logout/get-by-email);
  the pattern will need to be repeated as more API features are added.
- No automated regeneration of the Angular API client (it's a manual,
  on-demand `npm run generate:api` step) — no build-time or CI enforcement
  that it's in sync with the API.
