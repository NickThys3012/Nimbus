# Mail dashboard setup guide

Setup guide for the "Nimbus Mail" Grafana dashboard tracked in issue #134. Companion to
[`docs/nimbus-email-setup.md`](nimbus-email-setup.md) (Brevo/SMTP setup) — this document covers
making the *result* of that pipeline observable once it is sending real mail.

Follows the same provisioning pattern as the existing dashboards: see
[`infra/observability/grafana/dashboards/nimbus-api-overview.json`](../infra/observability/grafana/dashboards/nimbus-api-overview.json)
and `infra/observability/grafana/provisioning/dashboards/dashboards.yml`.

**Status: built.** The dashboard is provisioned at
[`infra/observability/grafana/dashboards/nimbus-mail-overview.json`](../infra/observability/grafana/dashboards/nimbus-mail-overview.json)
(`uid: nimbus-mail-overview`) — no manual Grafana setup is required, it loads automatically the
same way `nimbus-api-overview.json` does. The rest of this document describes the data it's built
on and how to verify it end to end.

## Prerequisites (done)

- #127 — `IEmailSender` / `SmtpEmailSender` (MailKit), which is where sends actually happen
- #128 — retry, failure handling and delivery logging, plus the `SentEmail` audit table

## Data sources

### 1. Structured logs (Loki) — required

`SmtpEmailSender` emits one Serilog message-template event per send attempt, distinct from generic
API request logs. The actual event names shipped in #128 (message-template prefixes, not a
dedicated `event` field) are:

- `EmailSent {Template} to {Recipient} attempt {Attempt} id {MessageId}` — succeeded
- `EmailRejected {Template} to {Recipient}: {Reason}` — permanent failure, no retry attempted
- `EmailRetry {Template} to {Recipient} attempt {Attempt} in {Delay}ms: {Reason}` — transient
  failure, will retry
- `EmailFailed {Template} to {Recipient} after {Attempts} attempts: {Reason}` — retries exhausted

These are *not* the `EmailSendSucceeded` / `EmailSendFailed` names originally proposed here before
#128 landed — the dashboard queries below use the real names. The `Serilog.Sinks.Grafana.Loki`
`LokiJsonTextFormatter` (in use via `Nimbus.Logging/DependencyInjection.cs`) puts every Serilog
property (`Template`, `Recipient`, `Attempt`, `Reason`, `MessageId`, …) as a top-level field in the
JSON log body, queryable with `| json` in LogQL, and injects `level` as a stream label. Recipient
addresses are still redacted by `SensitiveDataRedactionEnricher` before they reach Loki.

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

## The dashboard (`nimbus-mail-overview.json`)

Panels, all Loki-backed since there's no Prometheus counter yet:

- **Send rate (success vs failure)** — `timeseries`:
  `sum(count_over_time({app="nimbus-api"} |= "EmailSent " [5m]))` vs. `EmailRejected` +
  `EmailFailed` counts summed together (a rejection and an exhausted-retries failure are both
  "the mail didn't go out").
- **Failure ratio** — `timeseries`, failed / (failed + succeeded), same shape as the existing
  "5xx error ratio" panel.
- **Failures by reason** — `barchart`, `| json` then `by (Reason)`, split across `EmailRejected`
  and `EmailFailed` lines, so a provider-side outage (SMTP 5xx) is visually distinct from a bad
  recipient address.
- **Retry count (by attempt)** — `timeseries`, `EmailRetry` lines `| json` `by (Attempt)` — a
  rising share of `Attempt > 1` is an early warning before sends start failing outright.
- **Recent failures (logs panel)** — `logs`, `{app="nimbus-api"} |~ "EmailRejected |EmailFailed "`,
  so an on-call engineer can read the actual SMTP rejection reason without shelling into the box.

No provisioning change was needed — `dashboards.yml` already watches the whole `dashboards/`
directory, so the file is picked up automatically.

To verify locally: send a test mail through the Mailpit dev container (#131), confirm the log
event appears in Loki (`docker compose logs -f loki` or the Explore view in Grafana), then check
the panels render in the "Nimbus" folder in Grafana.

## Alerting (optional follow-up)

`infra/observability/alert.rules.yml` already has the pattern for Prometheus alerting rules. A
follow-up alert on a sustained failure ratio (e.g. `> 25%` over 15 minutes) would catch a Brevo
outage or a revoked SMTP key before a pilot reports a missing password-reset email — track this as
a separate issue rather than folding it into the dashboard's acceptance criteria.
