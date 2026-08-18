namespace Nimbus.Domain.Entities;

/// <summary>
///     An immutable audit record for a single email send attempt. Written for every call
///     into <c>IEmailSender.SendAsync</c>, success or failure, so a lost password reset or a
///     silently dropped notification is always visible after the fact (issue #128) —
///     independently of whatever the Loki/Grafana sink retains.
/// </summary>
public class SentEmail
{
    public Guid Id { get; private set; }
    public string Recipient { get; private set; }
    public string? Template { get; private set; }
    public DateTime SentAt { get; private set; }
    public bool Succeeded { get; private set; }
    public string? ProviderMessageId { get; private set; }
    public string? FailureReason { get; private set; }

    private SentEmail()
    {
        // EF Core materialization.
        Recipient = null!;
    }

    private SentEmail(
        string recipient,
        string? template,
        DateTime sentAt,
        bool succeeded,
        string? providerMessageId,
        string? failureReason)
    {
        Id = Guid.NewGuid();
        Recipient = recipient;
        Template = template;
        SentAt = sentAt;
        Succeeded = succeeded;
        ProviderMessageId = providerMessageId;
        FailureReason = failureReason;
    }

    public static SentEmail ForSuccess(string recipient, string? template, string? providerMessageId) =>
        new(recipient, template, DateTime.UtcNow, succeeded: true, providerMessageId, failureReason: null);

    public static SentEmail ForFailure(string recipient, string? template, string? failureReason) =>
        new(recipient, template, DateTime.UtcNow, succeeded: false, providerMessageId: null, failureReason);
}
