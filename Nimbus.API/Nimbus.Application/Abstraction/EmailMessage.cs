namespace Nimbus.Application.Abstraction;


/// <summary>
/// A single outbound message. Both an HTML and a plain-text body are required:
/// HTML-only messages score badly with spam filters and render as empty in
/// text-only clients.
/// </summary>
public sealed record EmailMessage
{
    public required string ToAddress { get; init; }
    public string? ToName { get; init; }
    public required string Subject { get; init; }
    public required string HtmlBody { get; init; }
    public required string TextBody { get; init; }

    /// <summary>Overrides the configured default reply-to when set.</summary>
    public string? ReplyToAddress { get; init; }

    /// <summary>Template identifier, used for logging and the audit trail.</summary>
    public string? Template { get; init; }
}
