using Microsoft.Extensions.Logging;
using Nimbus.Application.Abstraction;
using Nimbus.Application.Common.Interfaces;
using Nimbus.Domain.Entities;
using Nimbus.Infrastructure.Persistence;
namespace Nimbus.Infrastructure.Services;

/// <summary>
/// EF Core-backed <see cref="IEmailAuditLogger"/>. Persists a <see cref="SentEmail"/> row
/// per send attempt so a lost password reset or notification stays visible after the
/// fact (issue #128). Deliberately swallows persistence failures: the audit trail must
/// never turn an otherwise-successful (or already-logged-elsewhere) email send into a
/// 500 for the caller.
/// </summary>
public sealed class EmailAuditLogger : IEmailAuditLogger
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<EmailAuditLogger> _logger;

    public EmailAuditLogger(AppDbContext dbContext, ILogger<EmailAuditLogger> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task LogAsync(
        EmailMessage message,
        EmailSendResult result,
        CancellationToken cancellationToken = default)
    {
        var entry = result.Succeeded
            ? SentEmail.ForSuccess(message.ToAddress, message.Template, result.MessageId)
            : SentEmail.ForFailure(message.ToAddress, message.Template, result.FailureReason);

        try
        {
            _dbContext.SentEmails.Add(entry);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "EmailAuditWriteFailed {Template} to {Recipient}",
                message.Template ?? "adhoc",
                message.ToAddress);
        }
    }
}
