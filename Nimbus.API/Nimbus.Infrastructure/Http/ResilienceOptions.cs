namespace Nimbus.Infrastructure.Http;

/// <summary>
///     Failure policy of one provider (the <c>Resilience</c> subsection of its options). A transient failure (a network error,
///     a timeout, a 408, 429 or 5xx) is retried with exponential backoff and jitter; a provider that keeps failing trips a
///     circuit breaker so it is not hammered while it is down. Everything is configuration.
/// </summary>
public sealed class ResilienceOptions
{
    /// <summary>Extra attempts after the first one (0 turns retrying off).</summary>
    public int MaxRetries { get; init; } = 2;

    /// <summary>Delay before the first retry; it doubles each time and is spread by jitter.</summary>
    public int RetryBaseDelayMs { get; init; } = 300;

    /// <summary>Failed calls, within the sampling window, that open the circuit (at least half of the calls must have failed).</summary>
    public int BreakAfterFailures { get; init; } = 5;

    /// <summary>How long the circuit stays open before one call is let through to test the provider.</summary>
    public int BreakDurationSeconds { get; init; } = 30;

    /// <summary>The window the breaker looks at when counting failures.</summary>
    public int SamplingSeconds { get; init; } = 30;

    public string? Validate(string section)
    {
        if (MaxRetries is < 0 or > 5) return $"{section}:Resilience:MaxRetries must be between 0 and 5.";
        if (RetryBaseDelayMs is < 1 or > 10_000) return $"{section}:Resilience:RetryBaseDelayMs must be between 1 and 10000.";
        if (BreakAfterFailures is < 2 or > 100) return $"{section}:Resilience:BreakAfterFailures must be between 2 and 100.";
        if (BreakDurationSeconds is < 1 or > 600) return $"{section}:Resilience:BreakDurationSeconds must be between 1 and 600.";
        if (SamplingSeconds is < 1 or > 600) return $"{section}:Resilience:SamplingSeconds must be between 1 and 600.";
        return null;
    }
}

/// <summary>What one provider's HTTP client needs: its per-attempt timeout and its failure policy.</summary>
public sealed record ProviderHttpSettings(int TimeoutSeconds, ResilienceOptions Resilience);
