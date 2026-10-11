using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
namespace Nimbus.API.Security;

/// <summary>
///     Requires a valid antiforgery token (the <c>X-XSRF-TOKEN</c> header paired with the HttpOnly antiforgery
///     cookie) on every state-changing request, anonymous endpoints included (login CSRF). GET/HEAD/OPTIONS/TRACE
///     are exempt. Registered globally, so a new controller is protected by default.
///     (The framework's own <c>AutoValidateAntiforgeryTokenAttribute</c> needs the MVC view services, which an API does not have.)
/// </summary>
public sealed class AntiforgeryValidationFilter : IAsyncAuthorizationFilter
{
    private readonly IAntiforgery _antiforgery;

    public AntiforgeryValidationFilter(IAntiforgery antiforgery)
    {
        _antiforgery = antiforgery;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (HttpMethods.IsGet(context.HttpContext.Request.Method) || HttpMethods.IsHead(context.HttpContext.Request.Method) ||
            HttpMethods.IsOptions(context.HttpContext.Request.Method) || HttpMethods.IsTrace(context.HttpContext.Request.Method))
        {
            return;
        }

        try
        {
            await _antiforgery.ValidateRequestAsync(context.HttpContext);
        }
        catch (AntiforgeryValidationException)
        {
            context.Result = new BadRequestObjectResult(new ProblemDetails
            {
                Title = "The antiforgery token is missing or invalid.", Status = StatusCodes.Status400BadRequest
            });
        }
    }
}
