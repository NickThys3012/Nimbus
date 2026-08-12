using Nimbus.Logging;
using Serilog;
using Serilog.Events;
namespace Nimbus.Api.Tests;

/// <summary>
/// Guards issue #12's "no personal data, passwords, tokens or presigned URLs are
/// ever logged" acceptance criterion: renders representative log calls through
/// the real enricher pipeline and asserts sensitive values never appear in the
/// resulting output.
/// </summary>
public class SensitiveDataRedactionEnricherTests
{
    private static string Render(Action<ILogger> act)
    {
        var events = new List<LogEvent>();
        var logger = new LoggerConfiguration()
            .Enrich.With<SensitiveDataRedactionEnricher>()
            .WriteTo.Sink(new DelegatingSink(events.Add))
            .CreateLogger();

        act(logger);

        var writer = new StringWriter();
        foreach (var evt in events)
        {
            evt.RenderMessage(writer);
            writer.WriteLine();
            foreach (var (_, value) in evt.Properties)
            {
                writer.WriteLine(value.ToString());
            }
        }

        return writer.ToString();
    }

    [TestCase("Password", "hunter2")]
    [TestCase("password", "hunter2")]
    [TestCase("RefreshToken", "abc.def.ghi")]
    [TestCase("ApiKey", "placeholder-value-not-a-real-key")]
    [TestCase("Authorization", "Bearer abc123")]
    [TestCase("PresignedUrl", "https://minio.internal/bucket/obj?X-Amz-Signature=deadbeef")]
    public void RedactsKnownSensitivePropertyNames(string propertyName, string secretValue)
    {
        var output = Render(logger =>
            logger.ForContext(propertyName, secretValue).Information("Handling request"));

        Assert.That(output, Does.Not.Contain(secretValue));
        Assert.That(output, Does.Contain("REDACTED"));
    }

    [Test]
    public void DoesNotRedactOrdinaryProperties()
    {
        var output = Render(logger =>
            logger.ForContext("UserId", "42").Information("Fetched user {UserId}", "42"));

        Assert.That(output, Does.Contain("42"));
        Assert.That(output, Does.Not.Contain("REDACTED"));
    }

    private sealed class DelegatingSink(Action<LogEvent> write) : Serilog.Core.ILogEventSink
    {
        public void Emit(LogEvent logEvent) => write(logEvent);
    }
}
