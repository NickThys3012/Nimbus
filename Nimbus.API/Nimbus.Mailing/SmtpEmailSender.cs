using System.Net.Sockets;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using Nimbus.Application.Abstraction;
using Nimbus.Application.Common.Interfaces;
namespace Nimbus.Mailing;


/// <summary>
/// MailKit-backed sender. A fresh SmtpClient is created per mail: MailKit's
/// client is not thread-safe and must never be registered as a singleton.
/// </summary>
public sealed class SmtpEmailSender : IEmailSender
{
    private readonly EmailOptions _options;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(
        IOptions<EmailOptions> options,
        ILogger<SmtpEmailSender> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<EmailSendResult> SendAsync(
        EmailMessage message,
        CancellationToken cancellationToken = default)
    {
        var result = EmailSendResult.Permanent("not attempted");

        for (var attempt = 1; attempt <= _options.MaxAttempts; attempt++)
        {
            result = await TrySendAsync(message, cancellationToken);

            if (result.Succeeded)
            {
                _logger.LogInformation(
                    "EmailSent {Template} to {Recipient} attempt {Attempt} id {MessageId}",
                    message.Template ?? "adhoc",
                    message.ToAddress,
                    attempt,
                    result.MessageId);
                return result;
            }

            if (!result.IsTransient)
            {
                _logger.LogError(
                    "EmailRejected {Template} to {Recipient}: {Reason}",
                    message.Template ?? "adhoc",
                    message.ToAddress,
                    result.FailureReason);
                return result;
            }

            if (attempt >= _options.MaxAttempts)
            {
                continue;
            }
            
            var delay = TimeSpan.FromMilliseconds(
                _options.RetryBaseDelayMs * Math.Pow(2, attempt - 1));

            _logger.LogWarning(
                "EmailRetry {Template} to {Recipient} attempt {Attempt} in {Delay}ms: {Reason}",
                message.Template ?? "adhoc",
                message.ToAddress,
                attempt,
                delay.TotalMilliseconds,
                result.FailureReason);

            await Task.Delay(delay, cancellationToken);
        }

        _logger.LogError(
            "EmailFailed {Template} to {Recipient} after {Attempts} attempts: {Reason}",
            message.Template ?? "adhoc",
            message.ToAddress,
            _options.MaxAttempts,
            result.FailureReason);

        return result;
    }

    private async Task<EmailSendResult> TrySendAsync(
        EmailMessage message,
        CancellationToken cancellationToken)
    {
        using var client = new SmtpClient();
        client.Timeout = _options.TimeoutSeconds * 1000;

        try
        {
            var secureSocketOptions = _options.UseStartTls
                ? SecureSocketOptions.StartTls
                : SecureSocketOptions.None;

            await client.ConnectAsync(
                _options.SmtpHost,
                _options.SmtpPort,
                secureSocketOptions,
                cancellationToken);

            if (!string.IsNullOrWhiteSpace(_options.SmtpUser))
            {
                await client.AuthenticateAsync(
                    _options.SmtpUser,
                    _options.SmtpPassword ?? string.Empty,
                    cancellationToken);
            }

            var response = await client.SendAsync(
                BuildMimeMessage(message), cancellationToken);

            await client.DisconnectAsync(true, cancellationToken);

            return EmailSendResult.Success(ExtractMessageId(response));
        }
        catch (AuthenticationException ex)
        {
            // Bad credentials will not fix themselves on retry.
            return EmailSendResult.Permanent($"authentication failed: {ex.Message}");
        }
        catch (SmtpCommandException ex)
        {
            var permanent =
                ex.ErrorCode is SmtpErrorCode.RecipientNotAccepted
                    or SmtpErrorCode.SenderNotAccepted
                || (int)ex.StatusCode is >= 500 and < 600;

            return permanent
                ? EmailSendResult.Permanent($"{(int)ex.StatusCode} {ex.Message}")
                : EmailSendResult.Transient($"{(int)ex.StatusCode} {ex.Message}");
        }
        catch (Exception ex) when (
            ex is SmtpProtocolException
                or SocketException
                or IOException
                or TimeoutException)
        {
            return EmailSendResult.Transient($"{ex.GetType().Name}: {ex.Message}");
        }
    }

    private MimeMessage BuildMimeMessage(EmailMessage message)
    {
        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(_options.FromName, _options.FromAddress));
        mime.To.Add(new MailboxAddress(message.ToName ?? string.Empty, message.ToAddress));
        mime.Subject = message.Subject;

        var replyTo = message.ReplyToAddress ?? _options.ReplyToAddress;
        if (!string.IsNullOrWhiteSpace(replyTo))
        {
            mime.ReplyTo.Add(MailboxAddress.Parse(replyTo));
        }

        mime.Body = new BodyBuilder
        {
            HtmlBody = message.HtmlBody,
            TextBody = message.TextBody
        }.ToMessageBody();

        return mime;
    }

    /// <summary>
    /// Brevo returns the queued id in its 250 response, roughly
    /// "250 2.0.0 OK: queued as &lt;id&gt;". Anything unparseable is not an error.
    /// </summary>
    private static string? ExtractMessageId(string? response)
    {
        if (string.IsNullOrWhiteSpace(response)) return null;

        const string marker = "queued as ";
        var index = response.IndexOf(marker, StringComparison.OrdinalIgnoreCase);

        return index < 0
            ? response.Trim()
            : response[(index + marker.Length)..].Trim();
    }
}
