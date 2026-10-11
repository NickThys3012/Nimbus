using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Nimbus.Domain.Entities;
using Nimbus.Domain.Enums;
using Nimbus.Domain.Interfaces;
using Nimbus.Infrastructure.Persistence;
namespace Nimbus.Infrastructure.Tests;

public class LoginEventWriterServiceTests
{
    private static LoginEvent Event(string ip = "1.2.x.x")
    {
        return new LoginEvent(DateTime.UtcNow, null, null, false, ip, LoginFailureReason.InvalidPassword);
    }

    private static (LoginEventQueue Queue, LoginEventWriterService Writer, FakeRepository Repo) Create(FakeRepository? repo = null)
    {
        repo ??= new FakeRepository();
        var services = new ServiceCollection().AddSingleton<ILoginEventRepository>(repo).BuildServiceProvider();
        var queue = new LoginEventQueue(NullLogger<LoginEventQueue>.Instance);
        var writer = new LoginEventWriterService(queue, services.GetRequiredService<IServiceScopeFactory>(), NullLogger<LoginEventWriterService>.Instance);
        return (queue, writer, repo);
    }

    [Test]
    public async Task Shutdown_DrainsEverythingAlreadyQueued()
    {
        var (queue, writer, repo) = Create();
        for (var i = 0; i < 250; i++)
        {
            Assert.That(queue.TryEnqueue(Event()), Is.True);
        }

        await writer.StartAsync(CancellationToken.None);
        await writer.StopAsync(CancellationToken.None);

        Assert.That(repo.Written, Has.Count.EqualTo(250));
        Assert.That(queue.Pending, Is.Zero);
    }

    [Test]
    public async Task AfterShutdown_EnqueueReportsTheDrop_InsteadOfThrowing()
    {
        var (queue, writer, _) = Create();
        await writer.StartAsync(CancellationToken.None);
        await writer.StopAsync(CancellationToken.None);

        Assert.That(queue.TryEnqueue(Event()), Is.False);
        Assert.That(queue.Pending, Is.Zero);
    }

    [Test]
    public async Task FailingBatch_IsLogged_AndLaterEventsStillPersist()
    {
        var repo = new FakeRepository { FailFirstCall = true };
        var (queue, writer, _) = Create(repo);
        queue.TryEnqueue(Event("9.9.x.x"));
        await writer.StartAsync(CancellationToken.None);
        while (queue.Pending > 0)
        {
            await Task.Delay(10);
        }

        queue.TryEnqueue(Event("8.8.x.x"));
        await writer.StopAsync(CancellationToken.None);

        Assert.That(repo.Written.Select(e => e.MaskedIp), Is.EqualTo(new[] { "8.8.x.x" }));
    }

    private sealed class FakeRepository : ILoginEventRepository
    {
        private int _calls;
        public bool FailFirstCall { get; init; }
        public List<LoginEvent> Written { get; } = [];

        public Task AddRangeAsync(IReadOnlyCollection<LoginEvent> loginEvents, CancellationToken ct = default)
        {
            if (FailFirstCall && Interlocked.Increment(ref _calls) == 1)
            {
                throw new InvalidOperationException("db down");
            }

            Written.AddRange(loginEvents);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<LoginEvent>> ListRecentAsync(int limit, CancellationToken ct = default)
        {
            return Task.FromResult<IReadOnlyList<LoginEvent>>(Written);
        }
    }
}

