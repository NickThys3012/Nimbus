using Microsoft.AspNetCore.Identity;
using Nimbus.Application.Common.Interfaces;
using Nimbus.Application.Helpers;
using Nimbus.Infrastructure.Identity;
namespace Nimbus.API.Identity;

/// <summary>
///     Adapts Identity's <see cref="IEmailSender{TUser}" /> to the Nimbus mail queue, so anything
///     Identity itself wants to send goes through the same retry, audit and off-request delivery path.
/// </summary>
public sealed class IdentityEmailSender : Microsoft.AspNetCore.Identity.IEmailSender<ApplicationUser>
{
    private readonly IEmailQueue _queue;

    public IdentityEmailSender(IEmailQueue queue)
    {
        _queue = queue;
    }

    public Task SendConfirmationLinkAsync(ApplicationUser user, string email, string confirmationLink)
    {
        return _queue.EnqueueAsync(EmailLayout.Render(email, string.IsNullOrWhiteSpace(user.FirstName) ? "daar" : user.FirstName,
            "Bevestig je e-mailadres voor Nimbus", EmailTemplates.EmailConfirmation, "Bevestig je e-mailadres",
            ["Bevestig je e-mailadres om verder te gaan."], "E-mailadres bevestigen", confirmationLink)).AsTask();
    }

    public Task SendPasswordResetLinkAsync(ApplicationUser user, string email, string resetLink)
    {
        return _queue.EnqueueAsync(EmailHelpers.CreatePasswordReset(user.FirstName, email, resetLink)).AsTask();
    }

    // Nimbus resets through a link to the Angular route; the code-based flow is not offered.
    public Task SendPasswordResetCodeAsync(ApplicationUser user, string email, string resetCode)
    {
        throw new NotSupportedException("Password reset by code is not supported; use the link flow.");
    }
}
