/**
 * Accepts only same-origin, in-app paths as a post-login destination, so a crafted
 * `?returnUrl=https://evil.example` (or `//evil.example`) can never become an open redirect.
 */
export function safeReturnUrl(value: string | null | undefined, fallback = '/home'): string {
  if (!value || !value.startsWith('/') || value.startsWith('//') || value.includes('\\')) {
    return fallback;
  }
  return value;
}
