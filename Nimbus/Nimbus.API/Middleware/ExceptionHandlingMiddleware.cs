using System.Net;
using System.Text.Json;
using Nimbus.Application.Common.Exceptions;
using Nimbus.Domain.Exceptions;
namespace Nimbus.API.Middleware;

public class ExceptionHandlingMiddleware
{
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
            _logger.LogError(ex, "Unhandled exception");
            await HandleAsync(ctx, ex);
        }
    }

    private static async Task HandleAsync(HttpContext ctx, Exception ex)
    {
        var (status, title, errors) = ex switch
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

        ctx.Response.ContentType = "application/json";
        ctx.Response.StatusCode = (int)status;

        await ctx.Response.WriteAsync(JsonSerializer.Serialize(new
        {
            title, status = (int)status, errors
        }));
    }
}
