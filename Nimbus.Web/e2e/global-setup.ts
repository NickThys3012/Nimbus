import { execSync } from 'node:child_process';
import { existsSync, copyFileSync } from 'node:fs';
import path from 'node:path';

// Repo root is one level up from Nimbus.Web/.
const repoRoot = path.resolve(__dirname, '../..');
const envPath = path.join(repoRoot, '.env');
const envExamplePath = path.join(repoRoot, '.env.example');

export const composeArgs = [
  '-f',
  path.join(repoRoot, 'docker-compose.yml'),
  '-f',
  path.join(__dirname, 'docker-compose.e2e.yml'),
  '--profile',
  'dev',
];

/**
 * Brings up the full local stack (API, SQL Server, MinIO, Mailpit) with the
 * e2e email override applied, and waits for every service's healthcheck to
 * pass before handing control to the test run. Compose's own `--wait` does
 * the polling for us (using the healthchecks already defined in
 * docker-compose.yml) instead of us hand-rolling an HTTP poll loop here.
 *
 * Set E2E_SKIP_COMPOSE=1 to skip this (e.g. if you already have the stack
 * running locally via the commands documented in e2e/README.md) — useful
 * for fast local iteration without paying the full stack startup cost on
 * every run.
 */
export default async function globalSetup() {
  if (process.env.E2E_SKIP_COMPOSE === '1') {
    console.log('[e2e] E2E_SKIP_COMPOSE=1 — assuming the stack is already running.');
    return;
  }

  if (!existsSync(envPath)) {
    console.log('[e2e] No .env found at repo root — copying .env.example.');
    copyFileSync(envExamplePath, envPath);
  }

  console.log('[e2e] Starting docker compose stack (this can take a while, especially the first run/SQL Server cold start)...');
  execSync(`docker compose ${composeArgs.join(' ')} up --build -d --wait --wait-timeout 300`, {
    cwd: repoRoot,
    stdio: 'inherit',
  });
  console.log('[e2e] Stack is healthy.');
}
