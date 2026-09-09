# End-to-end tests (Playwright)

Real browser tests for the login/register/refresh/logout flow (issue #15),
run against the actual local Docker dev stack — not mocked. See
`global-setup.ts` for how the stack (API, SQL Server, MinIO, Mailpit) is
brought up, and `mailpit.ts` for how confirmation emails are read back out
via Mailpit's REST API instead of being faked.

## Running

```bash
npm run test:e2e
```

This brings the whole stack up itself (`docker compose ... --profile dev up
--build -d --wait`), runs the suite, then tears it down. First run is slow —
it builds the Docker images and waits out SQL Server's cold start (worse
under Rosetta on Apple Silicon, see the root `docker-compose.yml`'s own
comments).

### Faster local iteration

Leave the stack running between runs instead of paying the full
build+cold-start cost every time:

```bash
# First run: bring the stack up, keep it running afterwards
E2E_KEEP_STACK=1 npm run test:e2e

# Subsequent runs: reuse the already-running stack
E2E_SKIP_COMPOSE=1 npm run test:e2e

# When done
docker compose -f ../docker-compose.yml -f docker-compose.e2e.yml --profile dev down -v
```

## What this actually exercises

- Real registration through the UI, a real confirmation email captured by
  Mailpit (`docker-compose.e2e.yml` points the API's SMTP settings at it —
  the base `docker-compose.yml` doesn't send real mail by default), and
  following the real confirmation link.
- Real login, and asserting the `refreshToken` cookie Set by the API is
  actually `httpOnly`.
- A full page reload followed by navigating to a guarded route (`/settings`)
  — this only works if `authGuard`'s silent `restoreSession()` (a real
  `POST /api/authentication/refresh` using the httpOnly cookie) succeeds.
- "Logout": this currently calls `POST /api/authentication/logout` directly via `page.request`
  (shares the browser context's cookie jar) and then confirms the guarded route no longer works
  afterwards. Consider switching this to a UI click if/when the logout button is stable in the shell.
- Wrong-password handling and the 5-attempt account lockout.
