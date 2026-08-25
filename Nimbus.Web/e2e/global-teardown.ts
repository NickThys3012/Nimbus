import { execSync } from 'node:child_process';
import path from 'node:path';
import { composeArgs } from './global-setup';

const repoRoot = path.resolve(__dirname, '../..');

/**
 * Tears the stack back down after the run. Set E2E_KEEP_STACK=1 to leave it
 * running (handy while iterating on a spec locally — combine with
 * E2E_SKIP_COMPOSE=1 on the next run to reuse it). Mirrors E2E_SKIP_COMPOSE
 * in global-setup.ts: if you skipped bringing the stack up, don't tear down
 * a stack this run didn't start either.
 */
export default async function globalTeardown() {
  if (process.env.E2E_SKIP_COMPOSE === '1' || process.env.E2E_KEEP_STACK === '1') {
    console.log('[e2e] Leaving the docker compose stack running.');
    return;
  }

  console.log('[e2e] Tearing down docker compose stack...');
  execSync(`docker compose ${composeArgs.join(' ')} down -v`, {
    cwd: repoRoot,
    stdio: 'inherit',
  });
}
