using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nimbus.Application.Common.Interfaces;
namespace Nimbus.Mailing;

/// <summary>
/// Drains <see cref="EmailDeliveryQueue"/> and hands each message to the real
/// <see cref="IEmailSender"/> (SMTP/Null, wrapped with retry and audit logging) — this is
/// what actually moves sending off the request thread for issue #128.
///
/// Deliberately not a fire-and-forget <c>Task.Run</c> per request: that approach is lossy
/// on app shutdown (flagged as a known trade-off on <c>LoginEvent</c> in FlightPrep), and a
/// dropped mail is worse than a dropped login event since it's invisible to everyone,
/// including the user waiting for it. Instead, on <see cref="IHostApplicationLifetime.ApplicationStopping"/>
/// the queue stops accepting new work but this worker keeps draining what is already
/// queued — using <see cref="CancellationToken.None"/> for the read/send loop rather than
/// the host's stopping token — so in-flight and already-queued sends get to finish inside
/// the host's shutdown grace period instead of being cancelled mid-send.
/// </summary>
public sealed class EmailDeliveryWorker : BackgroundService
{
    private readonly EmailDeliveryQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<EmailDeliveryWorker> _logger;

    public EmailDeliveryWorker(
        EmailDeliveryQueue queue,
        IServiceScopeFactory scopeFactory,
        IHostApplicationLifetime lifetime,
        ILogger<EmailDeliveryWorker> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _lifetime = lifetime;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // This is a hosted service, never a UI/ASP.NET request context, so it must not
        // capture (or resume onto) whatever ambient SynchronizationContext the current
        // thread happens to have -- ConfigureAwait(false) throughout keeps continuations
        // on the thread pool regardless of caller.
        await using var stoppingRegistration = _lifetime.ApplicationStopping
            .Register(_queue.Complete)
            .ConfigureAwait(false);

        await foreach (var message in _queue.Reader.ReadAllAsync(CancellationToken.None).ConfigureAwait(false))
        {
            // IEmailSender is scoped (it ends up depending on AppDbContext for auditing via
            // the Auditing decorator), so each message gets its own DI scope rather than
            // sharing one DbContext across the worker's whole lifetime.
            await using var scope = _scopeFactory.CreateAsyncScope();
            var sender = scope.ServiceProvider.GetRequiredService<IEmailSender>();

            try
            {
                await sender.SendAsync(message, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // SendAsync itself never throws for a rejected/failed send (see
                // EmailSendResult) - this only catches something unexpected, e.g. a bug in
                // the sender or a DI failure, so one bad message cannot silently kill the
                // worker and strand everything queued behind it.
                _logger.LogError(
                    ex,
                    "EmailDeliveryWorkerCrashed {Template} to {Recipient}",
                    message.Template ?? "adhoc",
                    message.ToAddress);
            }
        }
    }
}
