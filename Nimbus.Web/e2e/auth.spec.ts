import { test, expect, type Page } from '@playwright/test';
import { waitForConfirmationLink } from './mailpit';

/**
 * End-to-end coverage for the full login/register/refresh/logout flow
 * against a real running stack (see playwright.config.ts / global-setup.ts).
 * Mirrors the backend integration suite in
 * Nimbus.API/Nimbus.Api.Tests/Integration/LoginFlowTests.cs, but drives the
 * actual Angular UI in a real browser instead of calling the API directly —
 * so it also exercises client-side routing/guards, not just the API.
 *
 * There is currently no logout button in the UI (navbar.html has a
 * "TODO: switch into a button based on the user logged in state" comment),
 * so the "logout" test below calls POST /api/authentication/logout directly
 * via page.request, which shares the browser context's cookie jar — this
 * still exercises the real cookie revocation, just not via a UI click.
 */

const password = 'Sup3r-Secret!';
// Deliberately wrong, but still satisfies the client-side complexity
// pattern (upper/lower/digit/special) — otherwise the Sign in button stays
// disabled client-side and the request never reaches the server at all.
const wrongPassword = 'Wr0ng-Password!';

function uniqueEmail(label: string): string {
  return `e2e-${label}-${Date.now()}-${Math.floor(Math.random() * 1e6)}@example.com`;
}

async function registerAndConfirm(page: Page, email: string, firstName = 'Ada', lastName = 'Lovelace') {
  await page.goto('/login');
  await page.getByText('Create account', { exact: true }).click();

  // `getByPlaceholder`/`getByLabel` alone would also match the custom
  // <Nimbus-input-field>/<Nimbus-password-field> host elements (their
  // `placeholder`/`label` template attributes are reflected onto the host
  // tag, not just the inner native input/the "Show password" toggle button
  // also has a matching aria-label) — scope to the actual <input> via
  // getByRole for text fields and the password field's fixed #pw-reg id.
  await page.getByRole('textbox', { name: 'FIRST NAME' }).fill(firstName);
  await page.getByRole('textbox', { name: 'LAST NAME' }).fill(lastName);
  await page.getByRole('textbox', { name: 'EMAIL ADDRESS' }).fill(email);
  await page.locator('input#pw-reg').fill(password);
  await page.getByRole('button', { name: 'Request Account' }).click();

  await expect(page).toHaveURL(/\/verification-email-sent/);

  const confirmLink = await waitForConfirmationLink(email);
  await page.goto(confirmLink);
  await expect(page).toHaveURL(/\/email-verification\?status=success/);
}

async function login(page: Page, email: string) {
  await page.goto('/login');
  await page.getByRole('textbox', { name: 'EMAIL ADDRESS' }).fill(email);
  await page.locator('input#pw-reg').fill(password);
  await page.getByRole('button', { name: 'Sign in' }).click();
  await expect(page).toHaveURL(/\/home/);
}

test.describe('Login / register / refresh / logout', () => {
  test('register -> confirm email -> login lands on /home', async ({ page }) => {
    const email = uniqueEmail('happy-path');

    await registerAndConfirm(page, email);
    await login(page, email);
  });

  test('login sets an httpOnly refresh cookie', async ({ page, context }) => {
    const email = uniqueEmail('cookie-attrs');
    await registerAndConfirm(page, email);
    await login(page, email);

    const cookies = await context.cookies();
    const refreshCookie = cookies.find((c) => c.name === 'refreshToken');

    expect(refreshCookie).toBeDefined();
    expect(refreshCookie?.httpOnly).toBe(true);
  });

  test('reloading the page silently restores the session via the refresh cookie', async ({
    page,
  }) => {
    const email = uniqueEmail('silent-refresh');
    await registerAndConfirm(page, email);
    await login(page, email);

    // The access token only ever lives in memory (AuthStore) — reloading
    // clears it, so landing on a guarded route afterwards only works if
    // authGuard's restoreSession() (POST /api/authentication/refresh using
    // the httpOnly cookie) actually succeeds.
    await page.reload();
    await page.goto('/settings');

    await expect(page).toHaveURL(/\/settings/);
  });

  test('logging out revokes the session so a guarded route redirects to /login', async ({
    page,
  }) => {
    const email = uniqueEmail('logout');
    await registerAndConfirm(page, email);
    await login(page, email);

    // No logout button exists in the UI yet — call the real endpoint
    // directly. page.request shares the browser context's cookie jar, so
    // this still exercises real server-side cookie/token revocation.
    const response = await page.request.post('/api/authentication/logout');
    expect(response.ok()).toBe(true);

    await page.reload();
    await page.goto('/settings');

    await expect(page).not.toHaveURL(/\/settings/);
  });

  test('logging in with the wrong password shows an error and stays on /login', async ({
    page,
  }) => {
    const email = uniqueEmail('bad-password');
    await registerAndConfirm(page, email);

    await page.goto('/login');
    await page.getByRole('textbox', { name: 'EMAIL ADDRESS' }).fill(email);
    await page.locator('input#pw-reg').fill(wrongPassword);
    await page.getByRole('button', { name: 'Sign in' }).click();

    await expect(page.locator('nimbus-banner#login-error')).toBeVisible();
    await expect(page).toHaveURL(/\/login/);
  });

  test('5 failed login attempts locks the account out', async ({ page }) => {
    const email = uniqueEmail('lockout');
    await registerAndConfirm(page, email);

    await page.goto('/login');
    for (let attempt = 0; attempt < 5; attempt++) {
      await page.getByRole('textbox', { name: 'EMAIL ADDRESS' }).fill(email);
      await page.locator('input#pw-reg').fill(wrongPassword);
      await page.getByRole('button', { name: 'Sign in' }).click();
      await expect(page.locator('nimbus-banner#login-error')).toBeVisible();
    }

    // A 6th attempt, this time with the *correct* password, should still be
    // rejected — the account is locked out regardless of credentials now.
    await page.getByRole('textbox', { name: 'EMAIL ADDRESS' }).fill(email);
    await page.locator('input#pw-reg').fill(password);
    await page.getByRole('button', { name: 'Sign in' }).click();

    await expect(page.locator('nimbus-banner#login-error')).toBeVisible();
    await expect(page).toHaveURL(/\/login/);
  });
});
