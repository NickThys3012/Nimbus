using Ganss.Xss;
using Nimbus.Application.Common.Interfaces;
namespace Nimbus.Application.Common;

/// <summary>
///     Allow-list sanitiser for the formatting the rich-text editor can produce: paragraphs, headings, bold, italic,
///     underline, strike, lists, quotes and links. No attribute survives except a link's <c>href</c>; every link gets
///     <c>rel="noopener noreferrer"</c>. Anything else is dropped, not escaped.
/// </summary>
public sealed class RichTextSanitizer : IRichTextSanitizer
{
    private static readonly string[] Tags =
        ["p", "br", "strong", "b", "em", "i", "u", "s", "h1", "h2", "h3", "ol", "ul", "li", "blockquote", "a"];

    private static readonly string[] Schemes = ["http", "https", "mailto", "tel"];

    private readonly HtmlSanitizer _sanitizer = Create();

    public string? Sanitize(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return null;

        var clean = _sanitizer.Sanitize(html).Trim();
        return HasVisibleContent(clean) ? clean : null;
    }

    private static HtmlSanitizer Create()
    {
        var s = new HtmlSanitizer();
        s.AllowedTags.Clear();
        foreach (var t in Tags) s.AllowedTags.Add(t);
        s.AllowedAttributes.Clear();
        s.AllowedAttributes.Add("href");
        s.AllowedClasses.Clear();
        s.AllowedCssProperties.Clear();
        s.AllowedSchemes.Clear();
        foreach (var scheme in Schemes) s.AllowedSchemes.Add(scheme);
        s.PostProcessNode += (_, e) =>
        {
            if (e.Node is AngleSharp.Dom.IElement { LocalName: "a" } a)
                a.SetAttribute("rel", "noopener noreferrer");
        };
        return s;
    }

    /// <summary>The editor's empty value is <c>&lt;p&gt;&lt;br&gt;&lt;/p&gt;</c>: markup with nothing to read.</summary>
    private static bool HasVisibleContent(string html)
    {
        var text = System.Text.RegularExpressions.Regex.Replace(html, "<[^>]*>", "");
        return !string.IsNullOrWhiteSpace(System.Net.WebUtility.HtmlDecode(text).Replace(' ', ' '));
    }
}
