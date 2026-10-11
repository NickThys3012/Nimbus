using Nimbus.Application.Abstraction;
using Nimbus.Application.Common.Interfaces;
namespace Nimbus.Application.Tests;

internal sealed class NoopQueue : IEmailQueue
{
    public ValueTask EnqueueAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        return ValueTask.CompletedTask;
    }
}
