using Microsoft.AspNetCore.Antiforgery;
namespace Nimbus.API.Security;

/// <summary>
///     Hands the SPA its antiforgery request token in a script-readable <c>XSRF-TOKEN</c> cookie (the
///     Angular HTTP client echoes it as <c>X-XSRF-TOKEN</c> on state-changing requests). The token is only
///     useful together with the HttpOnly antiforgery cookie, and is bound to the current identity, so it
///     must be re-issued whenever the identity changes (login, logout).
/// </summary>
public sealed class XsrfTokenWriter
{
    public const string CookieName = "XSRF-TOKEN";
    public const string HeaderName = "X-XSRF-TOKEN";

    private readonly IAntiforgery _antiforgery;
    private readonly IWebHostEnvironment _environment;

    public XsrfTokenWriter(IAntiforgery antiforgery, IWebHostEnvironment environment)
    {
        _antiforgery = antiforgery;
        _environment = environment;
    }

    public void Issue(HttpContext context)
    {
        var tokens = _antiforgery.GetAndStoreTokens(context);
        context.Response.Cookies.Append(CookieName, tokens.RequestToken!, new CookieOptions
        {
            HttpOnly = false, // must be readable by the SPA — it is not a credential on its own
            Secure = !_environment.IsDevelopment(),
            SameSite = SameSiteMode.Strict,
            IsEssential = true
        });
    }
}
