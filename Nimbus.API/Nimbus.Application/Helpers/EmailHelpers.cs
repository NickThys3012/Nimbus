using Nimbus.Application.Abstraction;
using Nimbus.Domain.Interfaces;
namespace Nimbus.Application.Helpers;

public class EmailHelpers
{
    private readonly IIdentityService _identityService;

    public EmailHelpers(IIdentityService identityService)
    {
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

        return EmailLayout.Render(email, GreetingName(firstName), "Bevestig je e-mailadres voor Nimbus", EmailTemplates.EmailConfirmation,
            "Bevestig je e-mailadres",
            ["Bedankt voor je registratie bij Nimbus. Bevestig je e-mailadres om verder te gaan."],
            "E-mailadres bevestigen", confirmUrl,
            "Heb je geen account aangemaakt? Dan kun je dit bericht negeren.");
    }

    public static EmailMessage CreateAccountPending(string firstName, string email)
    {
        return EmailLayout.Render(email, GreetingName(firstName), "Je Nimbus-account wacht op goedkeuring", EmailTemplates.AccountPending,
            "Je aanvraag is ontvangen",
            [
                "Bedankt voor je registratie bij Nimbus. Een beheerder bekijkt je aanvraag.",
                "Zodra je account is goedgekeurd, sturen we je een bericht."
            ]);
    }

    public static EmailMessage CreateAccountApproved(string firstName, string email)
    {
        return EmailLayout.Render(email, GreetingName(firstName), "Je Nimbus-account is goedgekeurd", EmailTemplates.AccountApproved,
            "Je account is goedgekeurd",
            ["Goed nieuws: een beheerder heeft je Nimbus-account goedgekeurd. Je kunt nu inloggen."]);
    }

    public static EmailMessage CreateAccountRejected(string firstName, string email)
    {
        return EmailLayout.Render(email, GreetingName(firstName), "Je aanvraag voor een Nimbus-account", EmailTemplates.AccountRejected,
            "Je aanvraag is niet goedgekeurd",
            ["Helaas is je aanvraag voor een Nimbus-account niet goedgekeurd.", "Denk je dat dit een vergissing is? Neem dan contact op met de beheerder."]);
    }

    public static EmailMessage CreatePasswordReset(string firstName, string email, string resetUrl)
    {
        return EmailLayout.Render(email, GreetingName(firstName), "Stel je Nimbus-wachtwoord opnieuw in", EmailTemplates.PasswordReset,
            "Wachtwoord opnieuw instellen",
            ["We kregen een verzoek om het wachtwoord van je Nimbus-account opnieuw in te stellen.", "De link werkt één keer en verloopt na een uur."],
            "Nieuw wachtwoord kiezen", resetUrl,
            "Heb je dit niet aangevraagd? Dan kun je dit bericht negeren; je wachtwoord blijft ongewijzigd.");
    }

    private static string GreetingName(string firstName)
    {
        return string.IsNullOrWhiteSpace(firstName) ? "daar" : firstName;
    }
}
