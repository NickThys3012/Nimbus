using System.Threading.Channels;
using Nimbus.Application.Abstraction;
using Nimbus.Application.Common.Interfaces;
namespace Nimbus.Mailing;

/// <summary>
/// In-process, unbounded queue backing <see cref="IEmailQueue"/>. A single instance is
/// shared (singleton) between callers, who only ever write, and
/// <see cref="EmailDeliveryWorker"/>, which is the sole reader.
/// </summary>
public sealed class EmailDeliveryQueue : IEmailQueue
{
    private readonly Channel<EmailMessage> _channel =
        Channel.CreateUnbounded<EmailMessage>(new UnboundedChannelOptions { SingleReader = true });

    public ChannelReader<EmailMessage> Reader => _channel.Reader;

    public ValueTask EnqueueAsync(EmailMessage message, CancellationToken cancellationToken = default) =>
        _channel.Writer.WriteAsync(message, cancellationToken);

    /// <summary>
    /// Stops accepting new work. Called once application shutdown begins so the worker's
    /// read loop can drain whatever is already queued and then exit naturally, rather than
    /// being cancelled mid-send.
    /// </summary>
    public void Complete() => _channel.Writer.TryComplete();
}
