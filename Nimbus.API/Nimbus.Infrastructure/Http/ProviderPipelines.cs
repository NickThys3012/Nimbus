using System.Collections.Concurrent;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;
namespace Nimbus.Infrastructure.Http;

/// <summary>
///     One resilience pipeline per provider, kept for the life of the app. It has to be a singleton: the circuit breaker's state
///     (how many calls failed lately, whether it is open) must outlive the short-lived HTTP handlers, and every client of one
///     provider (Open-Meteo has three) shares one circuit. Order, outermost first: retry, circuit breaker, per-attempt timeout.
/// </summary>
public sealed class ProviderPipelines
{
    private readonly ConcurrentDictionary<string, ResiliencePipeline<HttpResponseMessage>> _pipelines = new();

    public ResiliencePipeline<HttpResponseMessage> For(string provider, ProviderHttpSettings settings) =>
        _pipelines.GetOrAdd(provider, _ => Build(settings));

    /// <summary>Transient: worth another try, and a sign the provider is struggling. Not 4xx: that is our request being wrong.</summary>
    private static readonly PredicateBuilder<HttpResponseMessage> Transient = new PredicateBuilder<HttpResponseMessage>()
        .Handle<HttpRequestException>()
        .Handle<TimeoutRejectedException>()
        .HandleResult(r => (int)r.StatusCode >= 500 || r.StatusCode is System.Net.HttpStatusCode.RequestTimeout or System.Net.HttpStatusCode.TooManyRequests);

    public static ResiliencePipeline<HttpResponseMessage> Build(ProviderHttpSettings settings)
    {
        var r = settings.Resilience;
        var builder = new ResiliencePipelineBuilder<HttpResponseMessage>();

        if (r.MaxRetries > 0)
            builder.AddRetry(new RetryStrategyOptions<HttpResponseMessage>
            {
                ShouldHandle = Transient,
                MaxRetryAttempts = r.MaxRetries,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                Delay = TimeSpan.FromMilliseconds(r.RetryBaseDelayMs),
                // A response that is being retried is thrown away: release its connection.
                OnRetry = args =>
                {
                    args.Outcome.Result?.Dispose();
                    return default;
                }
            });

        builder.AddCircuitBreaker(new CircuitBreakerStrategyOptions<HttpResponseMessage>
        {
            ShouldHandle = Transient,
            FailureRatio = 0.5,
            MinimumThroughput = r.BreakAfterFailures,
            SamplingDuration = TimeSpan.FromSeconds(r.SamplingSeconds),
            BreakDuration = TimeSpan.FromSeconds(r.BreakDurationSeconds)
        });

        // Per attempt, not for the whole call: the retries each get the full timeout.
        builder.AddTimeout(TimeSpan.FromSeconds(settings.TimeoutSeconds));
        return builder.Build();
    }
}
