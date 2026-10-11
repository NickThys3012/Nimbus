using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Nimbus.Domain.Entities;
using Nimbus.Domain.Interfaces;
namespace Nimbus.Infrastructure.Persistence;

/// <summary>
///     Bounded in-memory hand-off between login requests and <see cref="LoginEventWriterService" />.
///     Writers never wait: a full or completed queue drops the event with a warning.
/// </summary>
public sealed class LoginEventQueue : ILoginEventQueue
{
    public const int Capacity = 10_000;

    private readonly Channel<LoginEvent> _channel = Channel.CreateBounded<LoginEvent>(
        new BoundedChannelOptions(Capacity)
        {
            SingleReader = true,
            FullMode = BoundedChannelFullMode.Wait
        });

    private readonly ILogger<LoginEventQueue> _logger;
    private int _pending;

    public LoginEventQueue(ILogger<LoginEventQueue> logger)
    {
        _logger = logger;
    }

    /// <summary>Events accepted but not yet persisted (or dropped by the writer). Zero means fully drained.</summary>
    public int Pending => Volatile.Read(ref _pending);

    internal ChannelReader<LoginEvent> Reader => _channel.Reader;

    public bool TryEnqueue(LoginEvent loginEvent)
    {
        Interlocked.Increment(ref _pending);
        if (_channel.Writer.TryWrite(loginEvent))
        {
            return true;
        }

        Interlocked.Decrement(ref _pending);
        _logger.LogWarning("Login event dropped: the audit queue is full or shutting down.");
        return false;
    }

    internal void MarkProcessed(int count)
    {
        Interlocked.Add(ref _pending, -count);
    }

    /// <summary>Stops accepting events; the reader still receives everything already queued.</summary>
    internal void Complete()
    {
        _channel.Writer.TryComplete();
    }
}
