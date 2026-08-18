using Microsoft.Extensions.Logging;
using Nimbus.Application.Abstraction;
using Nimbus.Application.Common.Interfaces;
namespace Nimbus.Mailing;

/// <summary>
/// Used when Email:Enabled is false. Logs what would have been sent so tests
/// and offline development do not silently lose messages.
/// </summary>
public sealed class NullEmailSender : IEmailSender
{
    private readonly ILogger<NullEmailSender> _logger;

    public NullEmailSender(ILogger<NullEmailSender> logger) => _logger = logger;

    public Task<EmailSendResult> SendAsync(
        EmailMessage message,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "EmailSuppressed {Template} to {Recipient} subject {Subject}",
            message.Template ?? "adhoc",
            message.ToAddress,
            message.Subject);

        return Task.FromResult(EmailSendResult.Success("suppressed"));
    }
}
