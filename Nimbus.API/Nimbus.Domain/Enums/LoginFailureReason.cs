namespace Nimbus.Domain.Enums;

/// <summary>Why a sign-in attempt was refused. Stored on <c>LoginEvent</c> for failed attempts only.</summary>
public enum LoginFailureReason
{
    InvalidPassword,
    NotApproved,
    LockedOut
}
