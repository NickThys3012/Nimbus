using Polly;
using Polly.CircuitBreaker;
using Polly.Timeout;
namespace Nimbus.Infrastructure.Http;

/// <summary>
///     Runs a provider's requests through its resilience pipeline. A call that still fails, because the circuit is open or the
///     last attempt timed out, surfaces as an <see cref="HttpRequestException" />: the one failure the provider clients already turn
///     into a failed result, so no Polly exception ever crosses into the application. A final 5xx comes back as the response, as
///     it would without retrying. Only requests that can be sent again (no body) are retried.
/// </summary>
public sealed class ResilienceHandler : DelegatingHandler
{
    private readonly ResiliencePipeline<HttpResponseMessage> _pipeline;

    public ResilienceHandler(ResiliencePipeline<HttpResponseMessage> pipeline)
    {
        _pipeline = pipeline;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        if (request.Content is not null)
            return await base.SendAsync(request, ct);

        try
        {
            var first = true;
            return await _pipeline.ExecuteAsync(async token =>
            {
                // A request message can be sent once: every retry sends a copy.
                var attempt = first ? request : Copy(request);
                first = false;
                return await base.SendAsync(attempt, token);
            }, ct);
        }
        catch (BrokenCircuitException ex)
        {
            throw new HttpRequestException($"The provider is not being called: its circuit is open after repeated failures ({request.RequestUri?.Host}).", ex);
        }
        catch (TimeoutRejectedException ex)
        {
            throw new HttpRequestException($"The provider did not answer in time ({request.RequestUri?.Host}).", ex);
        }
    }

    private static HttpRequestMessage Copy(HttpRequestMessage original)
    {
        var copy = new HttpRequestMessage(original.Method, original.RequestUri) { Version = original.Version, VersionPolicy = original.VersionPolicy };
        foreach (var header in original.Headers)
            copy.Headers.TryAddWithoutValidation(header.Key, header.Value);
        return copy;
    }
}
