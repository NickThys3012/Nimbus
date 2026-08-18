using Nimbus.Application.Abstraction;
using Nimbus.Application.Common.Interfaces;
namespace Nimbus.Mailing;

/// <summary>
/// Wraps the real <see cref="IEmailSender"/> (SMTP or Null) and writes a <c>SentEmail</c>
/// audit row after every attempt, success or failure (issue #128). Auditing is
/// best-effort: a failure to write the audit row must never turn a send that already
/// succeeded (or failed) on the wire into an unhandled exception for the caller.
/// </summary>
public sealed class AuditingEmailSender : IEmailSender
{
    private readonly IEmailSender _inner;
    private readonly IEmailAuditLogger _auditLogger;

    public AuditingEmailSender(IEmailSender inner, IEmailAuditLogger auditLogger)
    {
        _inner = inner;
        _auditLogger = auditLogger;
    }

    public async Task<EmailSendResult> SendAsync(
        EmailMessage message,
        CancellationToken cancellationToken = default)
    {
        var result = await _inner.SendAsync(message, cancellationToken);
        await _auditLogger.LogAsync(message, result, cancellationToken);
        return result;
    }
}
