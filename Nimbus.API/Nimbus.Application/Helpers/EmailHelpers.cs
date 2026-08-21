using System.Net;
using Nimbus.Application.Abstraction;
using Nimbus.Domain.Interfaces;
namespace Nimbus.Application.Helpers;

public class EmailHelpers
{
    private readonly IIdentityService _identityService;
    
    public EmailHelpers(IIdentityService identityService) {
        _identityService = identityService;
    }
    
    public async Task<EmailMessage> CreateEmailVerificationAsync(
        string id,
        string baseUrl,
        string firstName,
        string email)
    {
        var token = await _identityService.GenerateEmailConfrimTokenAsync(id);
        var confirmUrl = $"{baseUrl.TrimEnd('/')}/api/Authentication/confirm-email?userId={Uri.EscapeDataString(id)}&token={Uri.EscapeDataString(token)}";
        var greetingName = string.IsNullOrWhiteSpace(firstName) ? "there" : firstName;
        var safeFirstName = WebUtility.HtmlEncode(greetingName);

        return new EmailMessage
        {
            ToAddress = email,
            ToName = greetingName,
            Subject = "Verify your Nimbus email address",
            Template = "email-confirmation",
            TextBody =
                $"Hello {greetingName},\n\nThanks for registering with Nimbus.\nPlease verify your email by visiting this link:\n{confirmUrl}\n\nIf you did not create this account, you can ignore this email.",
            HtmlBody =
                $"<p>Hello {safeFirstName},</p><p>Thanks for registering with Nimbus.</p><p>Please verify your email by clicking <a href=\"{confirmUrl}\">this link</a>.</p><p>If you did not create this account, you can ignore this email.</p>"
        };
    }
}
