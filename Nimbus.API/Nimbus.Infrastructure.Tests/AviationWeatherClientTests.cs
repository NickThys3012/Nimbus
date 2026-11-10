using System.Net;
using Microsoft.Extensions.Logging;
using Nimbus.Infrastructure.Weather;
namespace Nimbus.Infrastructure.Tests;

/// <summary>Issue #44: aviationweather.gov through a stubbed handler. Every provider problem is a failed result and a logged warning; nothing throws.</summary>
public class AviationWeatherClientTests
{
    private static (AviationWeatherClient Client, Stub Stub, CapturedLog Log) Create(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond, TimeSpan? timeout = null)
    {
        var stub = new Stub(respond);
        var http = new HttpClient(stub) { BaseAddress = new Uri("https://aviationweather.gov/api/data/"), Timeout = timeout ?? TimeSpan.FromSeconds(5) };
        var log = new CapturedLog();
        return (new AviationWeatherClient(http, log), stub, log);
    }

    private static Task<HttpResponseMessage> Text(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });

    private static Task<HttpResponseMessage> Respond(HttpRequestMessage request, string metar, string taf) =>
        request.RequestUri!.AbsolutePath.EndsWith("/metar") ? Text(metar) : Text(taf);

    [Test]
    public async Task AsksForBothReportsInRawFormat_ForTheStation()
    {
        var (client, stub, _) = Create((r, _) => Respond(r, "METAR EBBR 1 2 3", "TAF EBBR 1 2 3"));

        var result = await client.GetRawAsync("EBBR");

        Assert.That(stub.Requests.Select(r => r.PathAndQuery), Is.EquivalentTo(new[] { "/api/data/metar?ids=EBBR&format=raw", "/api/data/taf?ids=EBBR&format=raw" }));
        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value!.MetarText, Is.EqualTo("METAR EBBR 1 2 3"));
        Assert.That(result.Value.TafText, Is.EqualTo("TAF EBBR 1 2 3"));
    }

    [Test]
    public async Task NoContent_AndAnEmptyBody_AreSuccessesWithNothingInThem_NotFailures()
    {
        var (client, _, log) = Create((r, _) => r.RequestUri!.AbsolutePath.EndsWith("/metar") ? Text("", HttpStatusCode.NoContent) : Text("   \n"));

        var result = await client.GetRawAsync("ZZZZ");

        Assert.That(result.IsSuccess, Is.True);
        Assert.That((result.Value!.MetarText, result.Value.TafText), Is.EqualTo(((string?)null, (string?)null)));
        Assert.That(log.Entries.Where(e => e.Level >= LogLevel.Warning), Is.Empty);
    }

    [TestCase(HttpStatusCode.InternalServerError)]
    [TestCase(HttpStatusCode.BadGateway)]
    [TestCase(HttpStatusCode.ServiceUnavailable)]
    [TestCase(HttpStatusCode.TooManyRequests)]
    [TestCase(HttpStatusCode.BadRequest)]
    public async Task AnErrorStatus_IsAFailedResult_NotAnException_AndALoggedWarning(HttpStatusCode status)
    {
        var (client, _, log) = Create((_, _) => Text("oops", status));

        var result = await client.GetRawAsync("EBBR");

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Value, Is.Null);
        Assert.That(result.Error, Does.Contain(((int)status).ToString()));
        var warning = log.Entries.First(e => e.Level == LogLevel.Warning);
        Assert.That(warning.Message, Does.Contain("EBBR").And.Contain(((int)status).ToString()));
    }

    [Test]
    public async Task AnUnreachableProvider_IsAFailedResultAndAWarning_WithTheExceptionAttached()
    {
        var (client, _, log) = Create((_, _) => throw new HttpRequestException("connection refused"));

        var result = await client.GetRawAsync("EBBR");

        Assert.That(result.IsSuccess, Is.False);
        var warning = log.Entries.First(e => e.Level == LogLevel.Warning);
        Assert.That(warning.Message, Does.Contain("could not be reached"));
        Assert.That(warning.Exception, Is.TypeOf<HttpRequestException>());
    }

    [Test]
    public async Task ASlowProvider_TimesOut_AsAFailedResult()
    {
        var (client, _, log) = Create(async (_, ct) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(5), ct);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }, timeout: TimeSpan.FromMilliseconds(80));

        var result = await client.GetRawAsync("EBBR");

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Error, Does.Contain("timed out"));
        Assert.That(log.Entries.First(e => e.Level == LogLevel.Warning).Message, Does.Contain("timed out"));
    }

    [Test]
    public void TheCallersOwnCancellation_IsNotTheProvidersFailure_AndPropagates()
    {
        using var cts = new CancellationTokenSource();
        var (client, _, log) = Create(async (_, ct) =>
        {
            await cts.CancelAsync();
            await Task.Delay(TimeSpan.FromSeconds(5), ct);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        Assert.CatchAsync<OperationCanceledException>(() => client.GetRawAsync("EBBR", cts.Token));
        Assert.That(log.Entries.Where(e => e.Level >= LogLevel.Warning), Is.Empty);
    }

    [Test]
    public async Task AnUnreasonablyLargeReply_IsRefused_AsAFailedResult()
    {
        var (client, _, _) = Create((r, _) => Text(new string('x', 300 * 1024)));

        var result = await client.GetRawAsync("EBBR");

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Error, Does.Contain("large"));
    }

    [Test]
    public async Task IfEitherReportFails_TheWholeFetchFails_SoThePilotIsToldInsteadOfGettingAHalfAnswer()
    {
        var (client, _, _) = Create((r, _) => r.RequestUri!.AbsolutePath.EndsWith("/metar") ? Text("METAR EBBR 1 2 3") : Text("down", HttpStatusCode.ServiceUnavailable));

        var result = await client.GetRawAsync("EBBR");

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Error, Does.StartWith("TAF"));
    }

    private sealed class Stub(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        private readonly List<Uri> _requests = [];
        public IReadOnlyList<Uri> Requests => _requests;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            lock (_requests) _requests.Add(request.RequestUri!);
            return respond(request, cancellationToken);
        }
    }

    private sealed class CapturedLog : ILogger<AviationWeatherClient>
    {
        private readonly List<(LogLevel Level, string Message, Exception? Exception)> _entries = [];
        public IReadOnlyList<(LogLevel Level, string Message, Exception? Exception)> Entries => _entries;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            lock (_entries) _entries.Add((logLevel, formatter(state, exception), exception));
        }
    }
}
