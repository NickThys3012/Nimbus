using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nimbus.Domain.Entities;
using Nimbus.Domain.Interfaces;
namespace Nimbus.Infrastructure.Persistence;

/// <summary>
///     Persists queued login events (issue #18). On shutdown the queue is closed and everything already
///     accepted is written before the host stops, so a recycle does not lose audit rows the way a
///     fire-and-forget <c>Task.Run</c> would. The host's shutdown timeout bounds how long the drain may take.
///     Implemented as a plain <see cref="IHostedService" /> rather than a <c>BackgroundService</c>: the latter
///     cancels its execute task on stop, and a stop that arrives before the loop was scheduled would skip the drain.
/// </summary>
public sealed class LoginEventWriterService : IHostedService
{
    private const int MaxBatch = 100;

    private readonly ILogger<LoginEventWriterService> _logger;
    private readonly LoginEventQueue _queue;
    private Task _loop = Task.CompletedTask;
    private readonly IServiceScopeFactory _scopes;

    public LoginEventWriterService(LoginEventQueue queue, IServiceScopeFactory scopes, ILogger<LoginEventWriterService> logger)
    {
        _queue = queue;
        _scopes = scopes;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _loop = Task.Run(DrainAsync, CancellationToken.None);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        // The loop ends once the closed queue is empty; the token only bounds how long we wait for it.
        _queue.Complete();
        await _loop.WaitAsync(cancellationToken);
    }

    private async Task DrainAsync()
    {
        var reader = _queue.Reader;
        var batch = new List<LoginEvent>(MaxBatch);
        while (await reader.WaitToReadAsync(CancellationToken.None))
        {
            while (batch.Count < MaxBatch && reader.TryRead(out var next))
            {
                batch.Add(next);
            }

            await WriteAsync(batch);
            _queue.MarkProcessed(batch.Count);
            batch.Clear();
        }
    }

    private async Task WriteAsync(List<LoginEvent> batch)
    {
        try
        {
            using var scope = _scopes.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<ILoginEventRepository>();
            await repository.AddRangeAsync(batch, CancellationToken.None);
        }
        catch (Exception ex)
        {
            // One bad batch must not stop the writer; the audit trail is best-effort beyond this point.
            _logger.LogError(ex, "Could not persist {Count} login events.", batch.Count);
        }
    }
}
