using Nimbus.Application.Abstraction;
namespace Nimbus.Application.Common.Interfaces;

public interface IEmailSender
{
    Task<EmailSendResult> SendAsync(
        EmailMessage message,
        CancellationToken cancellationToken = default);
}
