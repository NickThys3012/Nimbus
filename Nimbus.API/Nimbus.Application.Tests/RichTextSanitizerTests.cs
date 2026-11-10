using Nimbus.Application.Common;
namespace Nimbus.Application.Tests;

/// <summary>The briefing HTML is stored sanitised: the editor's formatting survives, everything executable does not.</summary>
public class RichTextSanitizerTests
{
    private readonly RichTextSanitizer _sut = new();

    [Test]
    public void EditorFormatting_Survives()
    {
        const string html = "<h3>Landing</h3><p>Hold <strong>on</strong>, <em>bend</em> your <u>knees</u> and <s>never</s> stand.</p><ul><li>One</li><li>Two</li></ul><ol><li>First</li></ol><blockquote>Listen to the pilot</blockquote>";
        Assert.That(_sut.Sanitize(html), Is.EqualTo(html));
    }

    [TestCase("<p>a</p><script>alert(1)</script>", "<p>a</p>")]
    [TestCase("<p onclick=\"alert(1)\">a</p>", "<p>a</p>")]
    [TestCase("<img src=x onerror=alert(1)><p>a</p>", "<p>a</p>")]
    [TestCase("<p>a</p><iframe src=\"https://evil.test\"></iframe>", "<p>a</p>")]
    [TestCase("<p style=\"background:url(javascript:alert(1))\">a</p>", "<p>a</p>")]
    [TestCase("<p class=\"ql-indent-1\">a</p>", "<p>a</p>")]
    [TestCase("<p>a</p><style>p{display:none}</style>", "<p>a</p>")]
    [TestCase("<span class=\"ql-ui\">x</span><p>a</p>", "<p>a</p>")]
    public void Executable_AndUnknownMarkup_IsRemoved(string input, string expected)
    {
        Assert.That(_sut.Sanitize(input), Is.EqualTo(expected));
    }

    [Test]
    public void AnUnknownElement_IsDroppedWithItsContent_NotUnwrapped()
    {
        // Safer than unwrapping: a script's code would otherwise survive as visible text.
        Assert.That(_sut.Sanitize("<p>a</p><div><p>b</p></div>"), Is.EqualTo("<p>a</p>"));
        Assert.That(_sut.Sanitize("<p>a</p><script>alert(1)</script>"), Does.Not.Contain("alert"));
    }

    [Test]
    public void Links_KeepAnHttpHref_AndGetRelNoopener()
    {
        var result = _sut.Sanitize("<p><a href=\"https://example.test/x\" target=\"_blank\" onclick=\"x()\">site</a></p>");
        Assert.That(result, Is.EqualTo("<p><a href=\"https://example.test/x\" rel=\"noopener noreferrer\">site</a></p>"));
    }

    [TestCase("javascript:alert(1)")]
    [TestCase("JaVaScRiPt:alert(1)")]
    [TestCase("data:text/html;base64,PHNjcmlwdD4=")]
    [TestCase("vbscript:x")]
    public void Links_WithAnUnsafeScheme_LoseTheirHref(string href)
    {
        var result = _sut.Sanitize($"<p><a href=\"{href}\">x</a></p>")!;
        Assert.That(result, Does.Not.Contain("href"));
        Assert.That(result, Does.Not.Contain("javascript").IgnoreCase);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase("<p><br></p>")]
    [TestCase("<p>&nbsp;</p>")]
    [TestCase("<script>alert(1)</script>")]
    public void NothingVisible_BecomesNull(string? input)
    {
        Assert.That(_sut.Sanitize(input), Is.Null);
    }
}
