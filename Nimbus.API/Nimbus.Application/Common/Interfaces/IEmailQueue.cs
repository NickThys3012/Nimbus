using Nimbus.Application.Abstraction;
namespace Nimbus.Application.Common.Interfaces;

/// <summary>
/// The entry point feature code should use to send email. Enqueues the message for
/// delivery off the HTTP request thread and returns once it is accepted onto the
/// queue, not once it is actually delivered — a slow SMTP handshake or a transient
/// provider blip must never become a slow API response (issue #128). Actual delivery,
/// retry, and audit logging happen on a background worker via <see cref="IEmailSender"/>.
/// </summary>
public interface IEmailQueue
{
    ValueTask EnqueueAsync(EmailMessage message, CancellationToken cancellationToken = default);
}
