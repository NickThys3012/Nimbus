using System.Net;
using System.Text.Encodings.Web;
using System.Text.Json;
using Nimbus.Application.Common.Exceptions;
using Nimbus.Domain.Exceptions;
namespace Nimbus.API.Middleware;

public class ExceptionHandlingMiddleware
{
    private static readonly JsonSerializerOptions ResponseJsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly RequestDelegate _next;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext ctx)
    {
        try
        {
            await _next(ctx);
        }
        catch (Exception ex)
        {
            var (status, title, errors) = MapException(ex);

            if (status == HttpStatusCode.InternalServerError)
            {
                _logger.LogError(ex, "Unhandled exception");
            }
            else
            {
                _logger.LogInformation(ex, "Handled exception mapped to HTTP {StatusCode}", (int)status);
            }

            await HandleAsync(ctx, status, title, errors);
        }
    }

    private static (HttpStatusCode Status, string Title, IDictionary<string, string[]>? Errors) MapException(Exception ex)
    {
        return ex switch
        {
            ValidationException ve => (HttpStatusCode.UnprocessableEntity,
                "Validation failed", ve.Errors),
            DomainException => (HttpStatusCode.BadRequest,
                ex.Message, null),
            NotFoundException => (HttpStatusCode.NotFound,
                ex.Message, null),
            _ => (HttpStatusCode.InternalServerError,
                "An unexpected error occurred.", null)
        };
    }

    private static async Task HandleAsync(
        HttpContext ctx,
        HttpStatusCode status,
        string title,
        IDictionary<string, string[]>? errors)
    {
        ctx.Response.ContentType = "application/json";
        ctx.Response.StatusCode = (int)status;

        await ctx.Response.WriteAsync(JsonSerializer.Serialize(new
        {
            title, status = (int)status, errors
        }, ResponseJsonOptions));
    }
}
