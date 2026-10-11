namespace Nimbus.Application.Helpers;

/// <summary>Template identifiers carried on <c>EmailMessage.Template</c> (audit trail and tests).</summary>
public static class EmailTemplates
{
    public const string EmailConfirmation = "email-confirmation";
    public const string AccountPending = "account-pending";
    public const string AccountApproved = "account-approved";
    public const string AccountRejected = "account-rejected";
    public const string PasswordReset = "password-reset";
}
