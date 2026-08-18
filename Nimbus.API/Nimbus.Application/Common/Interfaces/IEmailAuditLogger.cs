using Nimbus.Application.Abstraction;
namespace Nimbus.Application.Common.Interfaces;

/// <summary>
/// Persists a <c>SentEmail</c> audit row for every send attempt. Kept separate from
/// <see cref="IEmailSender"/> so Nimbus.Mailing never needs to depend on EF Core /
/// Nimbus.Infrastructure directly — the Infrastructure implementation writes to
/// <c>AppDbContext</c>, Mailing only calls this abstraction.
/// </summary>
public interface IEmailAuditLogger
{
    Task LogAsync(EmailMessage message, EmailSendResult result, CancellationToken cancellationToken = default);
}
