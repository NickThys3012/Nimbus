namespace Nimbus.API;

public static class AuthorizationPolicies
{
    /// <summary>Admin role AND an active (approved, not rejected) account.</summary>
    public const string AdminOnly = "AdminOnly";

    /// <summary>Any valid session, including pending/rejected accounts. Only for the minimal account endpoints (/me).</summary>
    public const string Authenticated = "Authenticated";

    public const string LoginRateLimit = "login";
}
