using System.Net;
using System.Text;
using Nimbus.Application.Abstraction;
namespace Nimbus.Application.Helpers;

/// <summary>
///     Renders the shared Nimbus transactional layout: a single-column, table-based HTML email with
///     inline CSS (the only styling mail clients reliably honour), plus the matching plain-text body.
/// </summary>
public static class EmailLayout
{
    private const string Accent = "#2563eb";
    private const string Ink = "#0f172a";
    private const string Muted = "#64748b";

    public static EmailMessage Render(
        string toAddress,
        string toName,
        string subject,
        string template,
        string heading,
        IReadOnlyList<string> paragraphs,
        string? buttonLabel = null,
        string? buttonUrl = null,
        string? footnote = null)
    {
        var greeting = $"Hallo {toName},";

        var html = new StringBuilder();
        html.Append("<!DOCTYPE html><html lang=\"nl\"><head><meta charset=\"utf-8\">")
            .Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">")
            .Append("<meta name=\"color-scheme\" content=\"light\">")
            .Append($"<title>{Enc(subject)}</title></head>")
            .Append($"<body style=\"margin:0;padding:0;background:#f1f5f9;font-family:-apple-system,'Segoe UI',Roboto,Helvetica,Arial,sans-serif;color:{Ink};\">")
            // Preheader: the grey preview line in the inbox list.
            .Append($"<div style=\"display:none;max-height:0;overflow:hidden;opacity:0;\">{Enc(paragraphs[0])}</div>")
            .Append("<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" style=\"background:#f1f5f9;padding:32px 12px;\"><tr><td align=\"center\">")
            .Append("<table role=\"presentation\" width=\"560\" cellpadding=\"0\" cellspacing=\"0\" style=\"width:100%;max-width:560px;background:#ffffff;border-radius:12px;overflow:hidden;border:1px solid #e2e8f0;\">")
            .Append($"<tr><td style=\"background:{Accent};padding:20px 32px;color:#ffffff;font-size:20px;font-weight:700;letter-spacing:.5px;\">Nimbus</td></tr>")
            .Append("<tr><td style=\"padding:32px;\">")
            .Append($"<h1 style=\"margin:0 0 16px;font-size:22px;line-height:1.3;color:{Ink};\">{Enc(heading)}</h1>")
            .Append($"<p style=\"margin:0 0 16px;font-size:16px;line-height:1.6;\">{Enc(greeting)}</p>");

        foreach (var paragraph in paragraphs)
        {
            html.Append($"<p style=\"margin:0 0 16px;font-size:16px;line-height:1.6;\">{Enc(paragraph)}</p>");
        }

        if (buttonLabel is not null && buttonUrl is not null)
        {
            html.Append("<table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\" style=\"margin:24px 0;\"><tr>")
                .Append($"<td style=\"background:{Accent};border-radius:8px;\">")
                .Append($"<a href=\"{Enc(buttonUrl)}\" style=\"display:inline-block;padding:14px 28px;font-size:16px;font-weight:600;color:#ffffff;text-decoration:none;\">{Enc(buttonLabel)}</a>")
                .Append("</td></tr></table>")
                .Append($"<p style=\"margin:0 0 16px;font-size:13px;line-height:1.5;color:{Muted};\">Werkt de knop niet? Kopieer deze link in je browser:<br><a href=\"{Enc(buttonUrl)}\" style=\"color:{Accent};word-break:break-all;\">{Enc(buttonUrl)}</a></p>");
        }

        if (footnote is not null)
        {
            html.Append($"<p style=\"margin:16px 0 0;font-size:13px;line-height:1.5;color:{Muted};\">{Enc(footnote)}</p>");
        }

        html.Append("</td></tr>")
            .Append($"<tr><td style=\"padding:16px 32px;background:#f8fafc;border-top:1px solid #e2e8f0;font-size:12px;color:{Muted};\">Dit is een automatisch bericht van Nimbus. Je ontvangt het omdat er een account of aanvraag bestaat met dit e-mailadres.</td></tr>")
            .Append("</table></td></tr></table></body></html>");

        var text = new StringBuilder();
        text.AppendLine(greeting).AppendLine();
        foreach (var paragraph in paragraphs)
        {
            text.AppendLine(paragraph).AppendLine();
        }

        if (buttonLabel is not null && buttonUrl is not null)
        {
            text.AppendLine($"{buttonLabel}: {buttonUrl}").AppendLine();
        }

        if (footnote is not null)
        {
            text.AppendLine(footnote).AppendLine();
        }

        text.AppendLine("-- Nimbus (automatisch bericht)");

        return new EmailMessage
        {
            ToAddress = toAddress, ToName = toName, Subject = subject, Template = template,
            HtmlBody = html.ToString(), TextBody = text.ToString()
        };
    }

    private static string Enc(string value)
    {
        return WebUtility.HtmlEncode(value);
    }
}
