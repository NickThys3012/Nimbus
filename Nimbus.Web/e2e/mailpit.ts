/**
 * Minimal client for the bits of Mailpit's REST API (https://mailpit.axllent.org/docs/api-v1/)
 * the e2e suite needs: finding the confirmation email sent to a given
 * address and pulling the confirm-email link out of its body.
 */

const MAILPIT_BASE_URL = process.env.E2E_MAILPIT_URL ?? 'http://localhost:8025';

interface MailpitMessageSummary {
  ID: string;
  To: { Address: string }[];
  Subject: string;
}

interface MailpitMessagesResponse {
  messages: MailpitMessageSummary[];
}

interface MailpitFullMessage {
  Text: string;
  HTML: string;
}

/**
 * Polls Mailpit for the most recent message sent to `toAddress` until one
 * shows up (or `timeoutMs` elapses), then extracts the
 * `/api/Authentication/confirm-email?userId=...&token=...` link from its
 * body (see Nimbus.Application/Helpers/EmailHelpers.cs — that's the exact
 * format this regex is matching).
 */
export async function waitForConfirmationLink(
  toAddress: string,
  { timeoutMs = 20_000, pollIntervalMs = 500 } = {},
): Promise<string> {
  const deadline = Date.now() + timeoutMs;

  while (Date.now() < deadline) {
    const message = await findLatestMessageTo(toAddress);
    if (message) {
      const link = extractConfirmationLink(await fetchFullMessage(message.ID));
      if (link) {
        return link;
      }
    }
    await new Promise((resolve) => setTimeout(resolve, pollIntervalMs));
  }

  throw new Error(`No confirmation email arrived for ${toAddress} within ${timeoutMs}ms`);
}

async function findLatestMessageTo(toAddress: string): Promise<MailpitMessageSummary | null> {
  const response = await fetch(
    `${MAILPIT_BASE_URL}/api/v1/messages?query=${encodeURIComponent(`to:${toAddress}`)}`,
  );
  if (!response.ok) {
    throw new Error(`Mailpit search failed: ${response.status} ${response.statusText}`);
  }
  const body = (await response.json()) as MailpitMessagesResponse;
  return body.messages[0] ?? null;
}

async function fetchFullMessage(id: string): Promise<MailpitFullMessage> {
  const response = await fetch(`${MAILPIT_BASE_URL}/api/v1/message/${id}`);
  if (!response.ok) {
    throw new Error(`Mailpit fetch message failed: ${response.status} ${response.statusText}`);
  }
  return (await response.json()) as MailpitFullMessage;
}

function extractConfirmationLink(message: MailpitFullMessage): string | null {
  const match = /https?:\/\/\S*\/api\/Authentication\/confirm-email\?userId=\S*&token=\S+/i.exec(
    message.Text || message.HTML,
  );
  return match ? match[0].replace(/["'<>]+$/, '') : null;
}

/** Deletes all captured messages — call between tests so a shared Mailpit
 *  instance doesn't leak messages from one test into another's search. */
export async function clearMailpit(): Promise<void> {
  await fetch(`${MAILPIT_BASE_URL}/api/v1/messages`, { method: 'DELETE' });
}
