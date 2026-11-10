namespace Nimbus.Application.Common.Interfaces;

/// <summary>Cleans user-authored rich text (HTML) before it is stored, so a stored value can never carry script.</summary>
public interface IRichTextSanitizer
{
    /// <summary>
    ///     Returns the HTML with everything outside the editor's formatting removed (scripts, event handlers, styles,
    ///     unknown tags, unsafe links). Null when the input is null or holds no visible content.
    /// </summary>
    string? Sanitize(string? html);
}
