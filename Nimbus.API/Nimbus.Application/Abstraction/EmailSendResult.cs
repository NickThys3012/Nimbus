namespace Nimbus.Application.Abstraction;


/// <summary>
/// Sends never throw on a rejected recipient. A bad address on one share
/// notification must not fail the surrounding request.
/// </summary>
public sealed record EmailSendResult
{
    public required bool Succeeded { get; init; }

    /// <summary>Provider message id, when the provider returned one.</summary>
    public string? MessageId { get; init; }

    public string? FailureReason { get; init; }

    /// <summary>
    /// True when the failure looked transient and a retry is worthwhile.
    /// False for permanent rejections such as an unknown mailbox.
    /// </summary>
    public bool IsTransient { get; init; }

    public static EmailSendResult Success(string? messageId) =>
        new() { Succeeded = true, MessageId = messageId };

    public static EmailSendResult Transient(string reason) =>
        new() { Succeeded = false, FailureReason = reason, IsTransient = true };

    public static EmailSendResult Permanent(string reason) =>
        new() { Succeeded = false, FailureReason = reason, IsTransient = false };
}
