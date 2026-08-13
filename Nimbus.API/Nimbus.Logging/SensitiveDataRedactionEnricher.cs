using Serilog.Core;
using Serilog.Events;
namespace Nimbus.Logging;

/// <summary>
///     Redacts well-known sensitive property names before a log event reaches any
///     sink (issue #12 AC: no personal data, passwords, tokens or presigned URLs are
///     ever logged). This runs after the message template has already captured
///     structured properties from `{PropertyName}` placeholders and `Enrich.With*`,
///     so it is the last line of defense regardless of which call site produced them.
///     It matches on property *name*, not value, deliberately: a per-value PII
///     classifier is unreliable, whereas "never log anything captured under a
///     property literally named Password/Token/..." is a rule call sites can be held
///     to and a test can enforce.
/// </summary>
public sealed class SensitiveDataRedactionEnricher : ILogEventEnricher
{

    private const string RedactedValue = "***REDACTED***";
    private static readonly string[] SensitiveNameFragments =
    [
        "password",
        "secret",
        "token",
        "apikey",
        "api_key",
        "authorization",
        "presignedurl",
        "presigned_url",
        "connectionstring",
        "creditcard",
        "ssn"
    ];

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        // Copy the keys first: mutating a dictionary while enumerating it throws.
        var sensitiveKeys = logEvent.Properties.Keys
            .Where(IsSensitiveName)
            .ToList();

        foreach (var key in sensitiveKeys)
        {
            logEvent.AddOrUpdateProperty(
                propertyFactory.CreateProperty(key, RedactedValue));
        }
    }

    private static bool IsSensitiveName(string propertyName)
    {
        var normalized = propertyName.Replace("_", string.Empty).ToLowerInvariant();
        return SensitiveNameFragments.Any(fragment =>
            normalized.Contains(fragment.Replace("_", string.Empty)));
    }
}
