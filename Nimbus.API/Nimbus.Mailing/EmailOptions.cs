using System.ComponentModel.DataAnnotations;
namespace Nimbus.Mailing;

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    /// <summary>
    /// When false, <see cref="NullEmailSender"/> is registered instead and
    /// messages are logged rather than sent.
    /// </summary>
    public bool Enabled { get; init; }

    [Required]
    public string SmtpHost { get; init; } = string.Empty;

    [Range(1, 65535)]
    public int SmtpPort { get; init; } = 587;

    /// <summary>Empty for local Mailpit, which accepts unauthenticated mail.</summary>
    public string? SmtpUser { get; init; }

    public string? SmtpPassword { get; init; }

    /// <summary>STARTTLS on 587. Set false only for local Mailpit.</summary>
    public bool UseStartTls { get; init; } = true;

    [Required, EmailAddress]
    public string FromAddress { get; init; } = string.Empty;

    public string FromName { get; init; } = "Nimbus";

    [EmailAddress]
    public string? ReplyToAddress { get; init; }

    [Range(1, 10)]
    public int MaxAttempts { get; init; } = 3;

    /// <summary>Base delay for exponential backoff between retries.</summary>
    [Range(100, 60_000)]
    public int RetryBaseDelayMs { get; init; } = 500;

    [Range(1, 300)]
    public int TimeoutSeconds { get; init; } = 30;
}
