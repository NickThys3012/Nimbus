namespace Nimbus.Domain.Interfaces;

public interface IIdentityService
{
    Task<string> RegisterAsync(string email, string password, string firstName, string lastName);
    Task<string> GenerateEmailConfrimTokenAsync(string id);

    /// <summary>
    ///     Sends a password-reset email when an eligible account exists for <paramref name="email" />.
    ///     Never reveals (by exception or return value) whether it did.
    /// </summary>
    Task RequestPasswordResetAsync(string email, string webBaseUrl, CancellationToken ct = default);

    /// <summary>Resets the password with a single-use token. Returns false for any invalid token or unknown account.</summary>
    Task<bool> ResetPasswordAsync(string email, string token, string newPassword, CancellationToken ct = default);
}
