# Mail dashboard setup guide

Setup guide for the "Nimbus Mail" Grafana dashboard tracked in issue #134. Companion to
[`docs/nimbus-email-setup.md`](nimbus-email-setup.md) (Brevo/SMTP setup) — this document covers
making the *result* of that pipeline observable once it is sending real mail.

Follows the same provisioning pattern as the existing dashboards: see
[`infra/observability/grafana/dashboards/nimbus-api-overview.json`](../infra/observability/grafana/dashboards/nimbus-api-overview.json)
and `infra/observability/grafana/provisioning/dashboards/dashboards.yml`.

## Prerequisites

This dashboard has nothing to query until the mail feature ships its instrumentation. Blocked on:

- #127 — `IEmailSender` / `SmtpEmailSender` (MailKit), which is where sends actually happen
- #128 — retry, failure handling and delivery logging: this issue's acceptance criteria
  ("Failures visible in Grafana via the Loki sink, with a distinct log event name that can be
  alerted on") and the `SentEmail` audit table are the two data sources this dashboard is built on

Do not start building the dashboard until at least #128 has landed — the log event name and any
Prometheus counters below are the *proposed* contract, not yet implemented; confirm the actual
field/event names against the merged code before wiring panels to them.

## Data sources

### 1. Structured logs (Loki) — required

`SmtpEmailSender` / the retry wrapper from #128 should emit one structured Serilog event per send
attempt, distinct from generic API request logs, e.g. `EmailSendSucceeded` / `EmailSendFailed`,
carrying at minimum: `recipient` (or a hashed/redacted form — see
`Nimbus.Logging/SensitiveDataRedactionEnricher.cs`, mail addresses are PII), `template`, `attempt`,
`outcome`, and `providerMessageId`. Confirm the exact event/field names in the merged #128 PR
before writing LogQL against them.

### 2. Business metrics (Prometheus) — optional, follow existing pattern

If a rate/counter view is wanted alongside the raw logs, extend `IBusinessMetrics`
(`Nimbus.Application/Common/Interfaces/IBusinessMetrics.cs`) and
`PrometheusBusinessMetrics` (`Nimbus.Observability/Services/PrometheusBusinessMetrics.cs`) the same
way `UserFetchedByEmail` already does:

```csharp
void EmailSendAttempted(string template, bool success);
```

```csharp
_emailSentCounter = metricsFactory.CreateCounter(
    "email_sent_total", "Number of transactional emails sent", "template", "outcome");
```

This is optional — the Loki logs alone are enough for a first version of the dashboard; add the
counter only if the log-volume panel proves too coarse.

## Building the dashboard

1. Copy `infra/observability/grafana/dashboards/nimbus-api-overview.json` as a starting point and
   change `id`, `uid` (e.g. `nimbus-mail-overview`) and `title` (`Nimbus Mail`).
2. Suggested panels:
   - **Send rate (success vs failure)** — `timeseries`, Loki:
     `sum(count_over_time({app="nimbus-api"} | json | event="EmailSendSucceeded" [5m]))` and the
     `EmailSendFailed` equivalent, or the Prometheus counter from above if added.
   - **Failure ratio** — `timeseries`, failed / (failed + succeeded) over a rolling window, same
     shape as the existing "5xx error ratio" panel.
   - **Failures by reason** — `timeseries` or `barchart`, Loki, `by (reason)` (SMTP response /
     exception type) so a provider-side outage is visually distinct from a bad recipient address.
   - **Retry count** — `timeseries`, Loki, `by (attempt)` — a rising share of `attempt > 1` sends
     is an early warning before sends start failing outright.
   - **Recent failures (logs panel)** — `logs`, `{app="nimbus-api"} | json | event="EmailSendFailed"`,
     so an on-call engineer can read the actual SMTP rejection reason without shelling into the box.
3. Save as `infra/observability/grafana/dashboards/nimbus-mail-overview.json`. No provisioning
   change needed — `dashboards.yml` already watches the whole `dashboards/` directory.
4. Verify locally: send a test mail through the Mailpit dev container (#131), confirm the log
   event appears in Loki (`docker compose logs -f loki` or the Explore view in Grafana), then
   confirm the panel renders before committing.

## Alerting (optional follow-up)

`infra/observability/alert.rules.yml` already has the pattern for Prometheus alerting rules. A
follow-up alert on a sustained failure ratio (e.g. `> 25%` over 15 minutes) would catch a Brevo
outage or a revoked SMTP key before a pilot reports a missing password-reset email — track this as
a separate issue rather than folding it into the dashboard's acceptance criteria.
