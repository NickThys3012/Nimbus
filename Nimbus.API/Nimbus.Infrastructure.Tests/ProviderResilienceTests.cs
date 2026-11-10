using System.Net;
using Nimbus.Infrastructure.Http;
namespace Nimbus.Infrastructure.Tests;

/// <summary>Issue #48: retry with backoff, per-attempt timeout and circuit breaker around a provider, through a stubbed handler.</summary>
public class ProviderResilienceTests
{
    private sealed class Stub(Func<int, HttpRequestMessage, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        public int Calls;
        public List<HttpRequestMessage> Requests { get; } = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add(request);
            var n = Interlocked.Increment(ref Calls);
            return await respond(n, request);
        }
    }

    private static HttpResponseMessage Status(HttpStatusCode s) => new(s) { Content = new StringContent("{}") };

    private static (HttpClient, Stub) Client(Func<int, HttpRequestMessage, Task<HttpResponseMessage>> respond, ResilienceOptions? r = null, int timeoutSeconds = 5)
    {
        var stub = new Stub(respond);
        var pipeline = ProviderPipelines.Build(new ProviderHttpSettings(timeoutSeconds, r ?? new ResilienceOptions { MaxRetries = 2, RetryBaseDelayMs = 1 }));
        var http = new HttpClient(new ResilienceHandler(pipeline) { InnerHandler = stub }) { BaseAddress = new Uri("https://provider.test/") };
        return (http, stub);
    }

    [Test]
    public async Task ATransientFailure_IsRetried_UntilItSucceeds()
    {
        var (http, stub) = Client((n, _) => Task.FromResult(Status(n < 3 ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK)));

        var response = await http.GetAsync("x");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(stub.Calls, Is.EqualTo(3));
    }

    [Test]
    public async Task EachRetry_SendsItsOwnRequestMessage_WithTheSameAddressAndHeaders()
    {
        var (http, stub) = Client((n, _) => Task.FromResult(Status(n < 2 ? HttpStatusCode.BadGateway : HttpStatusCode.OK)));
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Nimbus/1.0");

        await http.GetAsync("forecast?a=1");

        Assert.That(stub.Requests, Has.Count.EqualTo(2));
        Assert.That(stub.Requests[1], Is.Not.SameAs(stub.Requests[0]));
        Assert.That(stub.Requests[1].RequestUri, Is.EqualTo(stub.Requests[0].RequestUri));
        Assert.That(stub.Requests[1].Headers.UserAgent.ToString(), Is.EqualTo("Nimbus/1.0"));
    }

    [Test]
    public async Task AStillFailingProvider_IsGivenUpOnAfterTheConfiguredRetries_AndTheLastResponseReturned()
    {
        var (http, stub) = Client((_, _) => Task.FromResult(Status(HttpStatusCode.InternalServerError)));

        var response = await http.GetAsync("x");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.InternalServerError));
        Assert.That(stub.Calls, Is.EqualTo(3)); // the first call and two retries
    }

    [TestCase(HttpStatusCode.BadRequest)]
    [TestCase(HttpStatusCode.NotFound)]
    [TestCase(HttpStatusCode.Unauthorized)]
    public async Task AClientError_IsNotRetried(HttpStatusCode status)
    {
        var (http, stub) = Client((_, _) => Task.FromResult(Status(status)));

        var response = await http.GetAsync("x");

        Assert.That(response.StatusCode, Is.EqualTo(status));
        Assert.That(stub.Calls, Is.EqualTo(1));
    }

    [TestCase(HttpStatusCode.TooManyRequests)]
    [TestCase(HttpStatusCode.RequestTimeout)]
    public async Task ATooManyRequests_OrRequestTimeout_IsRetried(HttpStatusCode status)
    {
        var (http, stub) = Client((n, _) => Task.FromResult(Status(n == 1 ? status : HttpStatusCode.OK)));

        var response = await http.GetAsync("x");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(stub.Calls, Is.EqualTo(2));
    }

    [Test]
    public async Task ANetworkError_IsRetried_AndThenSurfacesAsAnHttpRequestException()
    {
        var (http, stub) = Client((_, _) => throw new HttpRequestException("no route"));

        Assert.ThrowsAsync<HttpRequestException>(() => http.GetAsync("x"));
        Assert.That(stub.Calls, Is.EqualTo(3));
        await Task.CompletedTask;
    }

    private sealed class SlowHandler : HttpMessageHandler
    {
        public int Calls;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Interlocked.Increment(ref Calls);
            await Task.Delay(TimeSpan.FromSeconds(30), ct); // like a real handler, it stops when cancelled
            return Status(HttpStatusCode.OK);
        }
    }

    [Test]
    public void ASlowAttempt_TimesOut_IsRetried_AndSurfacesAsAnHttpRequestException_NotAPollyException()
    {
        var slow = new SlowHandler();
        var pipeline = ProviderPipelines.Build(new ProviderHttpSettings(1, new ResilienceOptions { MaxRetries = 1, RetryBaseDelayMs = 1 }));
        var http = new HttpClient(new ResilienceHandler(pipeline) { InnerHandler = slow }) { BaseAddress = new Uri("https://provider.test/") };

        var ex = Assert.ThrowsAsync<HttpRequestException>(() => http.GetAsync("x"));

        Assert.That(ex!.Message, Does.Contain("did not answer in time"));
        Assert.That(slow.Calls, Is.EqualTo(2));
    }

    [Test]
    public async Task RetriesAreOff_WhenMaxRetriesIsZero()
    {
        var (http, stub) = Client((_, _) => Task.FromResult(Status(HttpStatusCode.ServiceUnavailable)), new ResilienceOptions { MaxRetries = 0 });

        await http.GetAsync("x");

        Assert.That(stub.Calls, Is.EqualTo(1));
    }

    [Test]
    public async Task AProviderThatKeepsFailing_OpensTheCircuit_AndIsNotCalledAgain()
    {
        var options = new ResilienceOptions { MaxRetries = 0, BreakAfterFailures = 3, BreakDurationSeconds = 60 };
        var (http, stub) = Client((_, _) => Task.FromResult(Status(HttpStatusCode.ServiceUnavailable)), options);

        for (var i = 0; i < 3; i++) await http.GetAsync("x");
        var callsWhenOpen = stub.Calls;
        var ex = Assert.ThrowsAsync<HttpRequestException>(() => http.GetAsync("x"));

        Assert.That(callsWhenOpen, Is.EqualTo(3));
        Assert.That(stub.Calls, Is.EqualTo(3), "an open circuit must not reach the provider");
        Assert.That(ex!.Message, Does.Contain("circuit is open"));
        await Task.CompletedTask;
    }

    [Test]
    public async Task AHealthyProvider_NeverOpensTheCircuit()
    {
        var (http, stub) = Client((_, _) => Task.FromResult(Status(HttpStatusCode.OK)), new ResilienceOptions { MaxRetries = 0, BreakAfterFailures = 2 });

        for (var i = 0; i < 10; i++) Assert.That((await http.GetAsync("x")).IsSuccessStatusCode, Is.True);

        Assert.That(stub.Calls, Is.EqualTo(10));
    }

    [Test]
    public async Task ARequestWithABody_IsSentOnce_NeverRetried()
    {
        var (http, stub) = Client((_, _) => Task.FromResult(Status(HttpStatusCode.ServiceUnavailable)));

        await http.PostAsync("x", new StringContent("q"));

        Assert.That(stub.Calls, Is.EqualTo(1));
    }

    [Test]
    public void OneProvider_HasOnePipeline_SoItsClientsShareACircuit_AndAnotherProviderHasItsOwn()
    {
        var pipelines = new ProviderPipelines();
        var s = new ProviderHttpSettings(5, new ResilienceOptions());

        Assert.That(pipelines.For("open-meteo", s), Is.SameAs(pipelines.For("open-meteo", s)));
        Assert.That(pipelines.For("open-meteo", s), Is.Not.SameAs(pipelines.For("aviation-weather", s)));
    }

    [Test]
    public async Task OneProviderBeingDown_DoesNotStopAnother()
    {
        var pipelines = new ProviderPipelines();
        var settings = new ProviderHttpSettings(5, new ResilienceOptions { MaxRetries = 0, BreakAfterFailures = 2, BreakDurationSeconds = 60 });
        var down = new Stub((_, _) => Task.FromResult(Status(HttpStatusCode.ServiceUnavailable)));
        var up = new Stub((_, _) => Task.FromResult(Status(HttpStatusCode.OK)));
        var downHttp = new HttpClient(new ResilienceHandler(pipelines.For("a", settings)) { InnerHandler = down }) { BaseAddress = new Uri("https://a.test/") };
        var upHttp = new HttpClient(new ResilienceHandler(pipelines.For("b", settings)) { InnerHandler = up }) { BaseAddress = new Uri("https://b.test/") };

        for (var i = 0; i < 2; i++) await downHttp.GetAsync("x");
        Assert.ThrowsAsync<HttpRequestException>(() => downHttp.GetAsync("x"));

        Assert.That((await upHttp.GetAsync("x")).IsSuccessStatusCode, Is.True);
    }

    [Test]
    public void TheCallersOwnCancellation_Propagates_AndIsNotRetried()
    {
        using var cts = new CancellationTokenSource();
        var (http, stub) = Client((_, _) =>
        {
            cts.Cancel();
            throw new OperationCanceledException(cts.Token);
        });

        Assert.CatchAsync<OperationCanceledException>(() => http.GetAsync("x", cts.Token));
        Assert.That(stub.Calls, Is.EqualTo(1));
    }
}
