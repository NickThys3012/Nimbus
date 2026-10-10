using System.Text;
namespace Nimbus.API;

/// <summary>
///     Fails fast at startup when the JWT signing key is missing or weak, so the API can never run
///     on a committed, default or guessable key. The real value is supplied per environment
///     (<c>Jwt__Secret</c>); Development has its own dev-only key in appsettings.Development.json.
/// </summary>
public static class JwtSecretValidator
{
    public const int MinimumBytes = 32;

    private static readonly string[] PlaceholderMarkers = ["REPLACE", "CHANGEME", "CHANGE-ME", "YOUR-", "YOUR_", "PLACEHOLDER", "EXAMPLE"];

    /// <summary>Returns the secret if acceptable; otherwise throws with a message that never contains the value.</summary>
    public static string Require(string? secret)
    {
        if (string.IsNullOrWhiteSpace(secret))
        {
            throw new InvalidOperationException(
                "Jwt:Secret is not configured. Set the Jwt__Secret environment variable (e.g. generate one with `openssl rand -base64 48`).");
        }

        if (PlaceholderMarkers.Any(m => secret.Contains(m, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("Jwt:Secret is a placeholder value. Set a real random secret in Jwt__Secret.");
        }

        if (Encoding.UTF8.GetByteCount(secret) < MinimumBytes)
        {
            throw new InvalidOperationException($"Jwt:Secret is too short: it must be at least {MinimumBytes} bytes.");
        }

        return secret;
    }
}
