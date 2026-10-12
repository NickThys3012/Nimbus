/**
 * Approves a freshly registered account through the real admin API. Since the
 * approval gate (#16) an unapproved account cannot sign in, so every e2e test
 * that logs in as a new user has to be approved first, just as in production.
 * The admin is seeded by docker-compose.e2e.yml.
 */

const API_BASE_URL = process.env.E2E_API_URL ?? 'http://localhost:8080';
const ADMIN_EMAIL = process.env.E2E_ADMIN_EMAIL ?? 'e2e-admin@nimbus.e2e';
const ADMIN_PASSWORD = process.env.E2E_ADMIN_PASSWORD ?? 'E2e-Admin-Pass1!';

interface AdminUser {
  id: string;
  email: string;
}

export async function approveUser(email: string): Promise<void> {
  const login = await fetch(`${API_BASE_URL}/api/authentication/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email: ADMIN_EMAIL, password: ADMIN_PASSWORD, rememberMe: false }),
  });
  if (!login.ok) {
    throw new Error(`E2E admin login failed: ${login.status} ${login.statusText}`);
  }
  const { accessToken } = (await login.json()) as { accessToken: string };
  const headers = { Authorization: `Bearer ${accessToken}` };

  const list = await fetch(`${API_BASE_URL}/api/admin/users`, { headers });
  if (!list.ok) {
    throw new Error(`E2E admin user list failed: ${list.status} ${list.statusText}`);
  }
  const user = ((await list.json()) as AdminUser[]).find(
    (u) => u.email.toLowerCase() === email.toLowerCase(),
  );
  if (!user) {
    throw new Error(`E2E could not find ${email} to approve`);
  }

  const approve = await fetch(`${API_BASE_URL}/api/admin/users/${user.id}/approve`, {
    method: 'POST',
    headers,
  });
  if (!approve.ok) {
    throw new Error(`E2E approve failed for ${email}: ${approve.status} ${approve.statusText}`);
  }
}
