# Nimbus

A .NET 10 API (Clean Architecture) paired with an Angular 21 (standalone components) SPA. See
[`docs/architecture.md`](../docs/architecture.md) for the full layer-by-layer breakdown — read it
before making non-trivial backend or frontend changes.

## Local development

```bash
cp .env.example .env
docker compose up --build
```

Runs the API (Angular built-in, served from the same origin), SQL Server, MinIO and Mailpit. See
the root [README.md](../README.md) for URLs and Apple Silicon notes.

## Build, test, lint

**Backend** (`Nimbus.API/`, solution file is `Nimbus.slnx`):

```bash
cd Nimbus.API
dotnet restore Nimbus.slnx
dotnet build Nimbus.slnx --no-restore -c Release -p:SkipSpaBuild=true   # skips Angular build
dotnet test Nimbus.slnx --no-build -c Release
```

Run a single test project or test: `dotnet test Nimbus.Domain.Tests/Nimbus.Domain.Tests.csproj
--filter "FullyQualifiedName~ClassName.MethodName"`. There are four test projects, one per layer
(`Nimbus.Api.Tests`, `Nimbus.Application.Tests`, `Nimbus.Domain.Tests`,
`Nimbus.Infrastructure.Tests`) — target the matching one instead of the whole solution when
iterating.

**Frontend** (`Nimbus.Web/`):

```bash
npm ci
npm run lint            # ng lint
npx ng test --no-watch  # single run; omit --no-watch for watch mode
npx ng build --configuration production
```

Run a single spec with `npx ng test --no-watch --include='**/foo.spec.ts'`.

CI (`.github/workflows/ci.yml`) runs `dotnet` and `angular` jobs in parallel, then a `publish` job
stitches the Angular production build into the API's `wwwroot/` — that combined artifact is what
actually gets deployed (`cd.yml`, triggered via `workflow_run` after CI succeeds on `main`).

## Architecture (brief — full detail in `docs/architecture.md`)

Backend is Clean Architecture, dependencies point inward:
`Nimbus.API → (Application, Infrastructure, Logging, Observability)`, `Infrastructure →
Application → Contracts → Domain`. `Domain` has zero dependencies.

- **Nimbus.Domain** — entities, enums, repository interfaces, domain exceptions. No framework deps.
- **Nimbus.Contracts** — DTOs and mappers shared between `Application`/`API`, also consumed
  indirectly by the Angular generated API client.
- **Nimbus.Application** — CQRS via **MediatR** + **FluentValidation**. Each use case is its own
  folder: `Features/<Feature>/Queries|Commands/<Name>/` containing the `Query`/`Command`,
  `Handler`, and `Validator` (e.g. `Features/Auth/Queries/GetUserByEmail/`). Validators run
  automatically via the `ValidationBehaviour` MediatR pipeline behaviour — don't call them
  manually in handlers.
- **Nimbus.Infrastructure** — EF Core (`AppDbContext`, SQL Server), ASP.NET Core Identity
  (`ApplicationUser`/`RefreshToken`, deliberately separate from `Domain.User`), health checks,
  object storage (`S3ObjectStorageService` over MinIO).
- **Nimbus.API** — thin controllers delegating to MediatR (`ISender`), except auth endpoints
  which call `UserManager`/`TokenService` directly (a known, intentional gap — not yet routed
  through CQRS).

Frontend (`Nimbus.Web/src/app/`):

- Standalone components only, no `NgModule`s. Routes are lazy-loaded (`loadComponent`) in
  `Nimbus.routes.ts`.
- **Generated API client** at `src/app/core/api-client/` — produced by
  `npm run generate:api` (`openapi-typescript-codegen`, requires the API running locally at
  `http://localhost:5214`). This directory is committed but auto-generated: never hand-edit it,
  and it's excluded from `eslint`.
- **Signals layer**: since the generated client is Observable-based, hand-written stores wrap it
  in Angular signals (see `core/auth/auth.store.ts` for the pattern) — mutations `.subscribe()`
  into writable `signal()`s; reactive queries use `rxResource()`. Follow this pattern for any new
  generated service consumed reactively; don't consume generated Observables directly from
  components.

## Conventions

- New backend use cases: add a `Features/<Feature>/<Queries|Commands>/<Name>/` folder with
  `<Name>Query|Command.cs`, `<Name>QueryHandler|CommandHandler.cs`, `<Name>Validator.cs` —
  mirror the existing `GetUserByEmail` slice.
- DTOs and entity↔DTO mapping go in `Nimbus.Contracts`, not `Nimbus.Application`.
- Migrations must be additive/backward-compatible with the immediately previous release —
  rollback is image-only (no schema rollback); never add and drop a column in the same release.
- EF Core migrations are applied by the dedicated `migrator` container, not by the API at
  startup (except `SeedUsers`, which the API does run itself).
- `dotnet build` treats warnings as errors in CI.
