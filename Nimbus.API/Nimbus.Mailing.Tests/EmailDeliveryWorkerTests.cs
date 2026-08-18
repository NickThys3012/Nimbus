using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Nimbus.Application.Abstraction;
using Nimbus.Application.Common.Interfaces;
namespace Nimbus.Mailing.Tests;

/// <summary>
///     Tests for the off-the-request-thread delivery path (issue #128): messages enqueued
///     onto <see cref="EmailDeliveryQueue" /> are handed to <see cref="IEmailSender" /> by
///     <see cref="EmailDeliveryWorker" />, and already-queued messages still get delivered
///     once application shutdown begins.
/// </summary>
[TestFixture]
public class EmailDeliveryWorkerTests
{
    private static EmailMessage Message(string to) => new()
    {
        ToAddress = to,
        Subject = "Test",
        HtmlBody = "<p>hi</p>",
        TextBody = "hi",
        Template = "test"
    };

    private static (EmailDeliveryWorker Worker, EmailDeliveryQueue Queue, FakeHostApplicationLifetime Lifetime, ConcurrentQueue<EmailMessage> Sent)
        CreateSut()
    {
        var sent = new ConcurrentQueue<EmailMessage>();
        var services = new ServiceCollection();
        services.AddSingleton(sent);
        services.AddScoped<IEmailSender, RecordingEmailSender>();
        var provider = services.BuildServiceProvider();

        var queue = new EmailDeliveryQueue();
        var lifetime = new FakeHostApplicationLifetime();
        var worker = new EmailDeliveryWorker(
            queue,
            provider.GetRequiredService<IServiceScopeFactory>(),
            lifetime,
            NullLogger<EmailDeliveryWorker>.Instance);

        return (worker, queue, lifetime, sent);
    }

    [Test]
    public async Task Worker_DeliversQueuedMessages_ViaEmailSender()
    {
        var (worker, queue, lifetime, sent) = CreateSut();

        await queue.EnqueueAsync(Message("a@example.com"));
        await queue.EnqueueAsync(Message("b@example.com"));

        await worker.StartAsync(CancellationToken.None);

        await WaitUntilAsync(() => sent.Count >= 2);

        lifetime.StopApplication();
        await worker.StopAsync(CancellationToken.None);

        Assert.That(sent.Select(m => m.ToAddress), Is.EquivalentTo(["a@example.com", "b@example.com"]));
    }

    [Test]
    public async Task Worker_DrainsAlreadyQueuedMessages_OnceApplicationStoppingFires()
    {
        var (worker, queue, lifetime, sent) = CreateSut();

        await worker.StartAsync(CancellationToken.None);
        await WaitUntilWorkerHasStartedAsync();

        // Simulate messages queued right as shutdown begins: they must still be
        // delivered, not dropped, once ApplicationStopping fires.
        await queue.EnqueueAsync(Message("queued-before-shutdown@example.com"));
        lifetime.StopApplication();

        await worker.StopAsync(CancellationToken.None);
        await WaitUntilAsync(() => !sent.IsEmpty);

        Assert.That(sent.Select(m => m.ToAddress), Does.Contain("queued-before-shutdown@example.com"));
    }

    [Test]
    public async Task Queue_RejectsNewWork_AfterApplicationStoppingFires()
    {
        var (worker, queue, lifetime, _) = CreateSut();

        await worker.StartAsync(CancellationToken.None);
        await WaitUntilWorkerHasStartedAsync();

        lifetime.StopApplication();
        await worker.StopAsync(CancellationToken.None);

        Assert.ThrowsAsync<ChannelClosedException>(async () => await queue.EnqueueAsync(Message("too-late@example.com")));
    }

    private static async Task WaitUntilAsync(Func<bool> condition, int timeoutMs = 2000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }
    }

    /// <summary>
    ///     <see cref="BackgroundService.StartAsync" /> schedules <c>ExecuteAsync</c> via
    ///     <c>Task.Run(..., stoppingToken)</c>: if that token is cancelled before the
    ///     thread-pool work item begins, the delegate never runs at all (see
    ///     dotnet/runtime's <c>BackgroundService.cs</c>). That race only matters in tests
    ///     that trigger shutdown in the same tick as start — real shutdowns always happen
    ///     long after the worker is up — so tests give the thread pool a moment to actually
    ///     start the work item before simulating <c>ApplicationStopping</c>.
    /// </summary>
    private static Task WaitUntilWorkerHasStartedAsync() => Task.Delay(100);

    private sealed class RecordingEmailSender : IEmailSender
    {
        private readonly ConcurrentQueue<EmailMessage> _sent;

        public RecordingEmailSender(ConcurrentQueue<EmailMessage> sent) => _sent = sent;

        public Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            _sent.Enqueue(message);
            return Task.FromResult(EmailSendResult.Success("test"));
        }
    }

    private sealed class FakeHostApplicationLifetime : IHostApplicationLifetime, IDisposable
    {
        private readonly CancellationTokenSource _stopping = new();

        public CancellationToken ApplicationStarted => CancellationToken.None;
        public CancellationToken ApplicationStopping => _stopping.Token;
        public CancellationToken ApplicationStopped => CancellationToken.None;

        public void StopApplication() => _stopping.Cancel();

        public void Dispose() => _stopping.Dispose();
    }
}
