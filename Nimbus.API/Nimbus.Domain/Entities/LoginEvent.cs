using Nimbus.Domain.Enums;
namespace Nimbus.Domain.Entities;

/// <summary>
///     Immutable audit record of one sign-in attempt (issue #19, admin "Aanmeldgeschiedenis").
///     Only the masked IP is stored (e.g. <c>84.198.x.x</c>). The email is a snapshot of the
///     matched account and is <c>null</c> when the attempt did not match any account, so
///     mistyped credentials are never persisted.
/// </summary>
public class LoginEvent
{
    private LoginEvent()
    {
        // EF Core materialization.
    }

    public LoginEvent(DateTime occurredAt, Guid? userId, string? email, bool succeeded, string? maskedIp,
        LoginFailureReason? failureReason = null)
    {
        Id = Guid.NewGuid();
        OccurredAt = occurredAt;
        UserId = userId;
        Email = email;
        Succeeded = succeeded;
        MaskedIp = maskedIp;
        FailureReason = failureReason;
    }

    public Guid Id { get; private set; }
    public DateTime OccurredAt { get; private set; }
    public Guid? UserId { get; private set; }
    public string? Email { get; private set; }
    public bool Succeeded { get; private set; }
    public string? MaskedIp { get; private set; }

    /// <summary>Set for failed attempts only.</summary>
    public LoginFailureReason? FailureReason { get; private set; }
}
