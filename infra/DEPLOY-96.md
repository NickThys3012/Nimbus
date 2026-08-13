# Deploying issue #96 (multi-stage production image) to the VPS

This is the delta on top of `nimbus-issue-5-STEPS.md` Part D, `infra/DEPLOY-103.md` and
`infra/DEPLOY-95.md` — you already have `/opt/nimbus` provisioned and the stack reachable. Unlike
those, **there is nothing to change on the VPS itself for #96**: it only changes how the `nimbus-api`
image is *built* (single `Dockerfile`, root of the repo), not any compose file, `.env` key, or
running service. This guide exists to record what changed and how it was verified, and to spell out
the one thing to double-check the first time the real image reaches `--profile app`.

## What changed

- `Dockerfile` — the `api` target now:
  - bakes `release-notes.json` (repo root) into `wwwroot/release-notes.json` at build time, so the
    future in-app changelog page (#74) can never drift from what's actually deployed.
  - runs a Skia render as a `RUN` step in the final stage, as the same non-root `nimbus` user the
    container starts as, before the image is ever pushed — see "Native dependency note" below.
- `Nimbus.API.csproj` — added `SkiaSharp` + `SkiaSharp.NativeAssets.Linux.NoDependencies` (needed by
  the trajectory/PDF map rendering work in #57/#62; brought in now so the image's native
  dependencies are proven ahead of that feature code).
- `Program.cs` — a `--render-smoke-test` CLI switch that renders a tiny bitmap and exits 0/1 with no
  web host and no database, so the Dockerfile can fail the build instead of failing the first real
  map render in production.
- `.dockerignore` — excludes `node_modules`, `bin`, `obj`, `dist`, `.git` from the build context.

## Native dependency note (the actual point of this issue)

`mcr.microsoft.com/dotnet/aspnet:10.0` is Ubuntu 24.04 (Noble). The dynamically-linked
`SkiaSharp.NativeAssets.Linux` package fails there with `undefined symbol` errors
(`FT_Get_BDF_Property`, then `uuid_generate_random`/`uuid_unparse`) even after installing
`libfontconfig1`/`libfreetype6`/`libuuid1` — `libSkiaSharp.so` expects those symbols to already be
resolvable in the process's global symbol scope rather than declaring them as `NEEDED`, and hits them
inconsistently depending on which user/thread first touches the font manager. Verified interactively
with `LD_DEBUG=libs,symbols` against this exact base image before concluding it wasn't a missing-apt-
package problem.

**Fix:** use `SkiaSharp.NativeAssets.Linux.NoDependencies` instead — it statically links
freetype/fontconfig/harfbuzz, so no extra `apt-get install` is needed in the final stage at all. The
`RUN ... --render-smoke-test` step in the Dockerfile is what caught this and now guards against a
regression (e.g. someone switching back to the dynamic package, or a future base-image bump changing
the ABI again).

## Image size baseline (record in the PR description)

```bash
docker build --target api -t nimbus-api:96 .
docker images nimbus-api:96 --format "{{.Size}}"
```

Measured locally (arm64): **837MB**. Watch this number on future PRs — a jump usually means a stray
SDK/Node artifact leaked into the final stage.

`.github/workflows/docker-image-size.yml` builds the `api` target on every relevant PR/push and
fails if it exceeds a 1024MB budget, printing the measured size to the job summary either way.

## Verifying before the CD pipeline exists

No CI workflow publishes `nimbus-api`/`nimbus-migrator` to GHCR yet (that's #6) — so nothing here is
rolled out to the VPS through this issue. To confirm the image still boots correctly against the real
stack once #6 lands, or if you want to check by hand in the meantime:

```bash
# from the VPS, or any box that can reach the sqlserver container's published port
docker run --rm \
  -e ConnectionStrings__Database="Server=<sqlserver-host>,1433;Database=Nimbus;User Id=nimbus_app;Password=<pwd>;TrustServerCertificate=True" \
  -e Jwt__Secret=<...> -e Jwt__Issuer=<...> -e Jwt__Audience=<...> \
  -p 8080:8080 -e ASPNETCORE_URLS=http://+:8080 \
  ghcr.io/nickthys3012/nimbus-api:<tag>
curl -fsS http://localhost:8080/health
```

Confirm `/health` is `200`, the Angular app loads at `/`, and `/release-notes.json` returns the file
baked in at build time (not a live-mounted one).

## Troubleshooting

| Symptom | Cause | Fix |
|---|---|---|
| `dotnet: symbol lookup error: ... libSkiaSharp.so: undefined symbol: ...` at build time | Someone switched back to `SkiaSharp.NativeAssets.Linux` (dynamic) | Use `SkiaSharp.NativeAssets.Linux.NoDependencies` in `Nimbus.API.csproj` |
| `RUN ... --render-smoke-test` step fails on a future base-image bump | ABI drift in the new Ubuntu base | Re-run the `LD_DEBUG=libs,symbols` investigation above against the new base tag before assuming it's an apt-package gap |
| `/release-notes.json` 404s | `release-notes.json` missing from repo root, or not copied by the `api-build` stage `COPY` | Confirm the file exists at the repo root and the Dockerfile's `COPY release-notes.json ...` line wasn't dropped |
