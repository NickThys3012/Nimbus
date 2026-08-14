using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Nimbus.API.Middleware;

/// <summary>
///     Response writer for <c>/health/live</c> and <c>/health/ready</c> (issue #97). Both
///     endpoints are anonymous, so the body must never leak anything about the process beyond
///     "can it serve or not" — no check names (which would reveal that SQL Server/MinIO back
///     this API), no exception messages, no version string, no connection details. Just the
///     overall <see cref="HealthStatus" /> as plain text, which is all the deploy gate, the
///     compose healthcheck and the uptime monitor need to make their decision.
/// </summary>
public static class HealthEndpointResponseWriter
{
    public static Task WriteAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "text/plain";
        return context.Response.WriteAsync(report.Status.ToString());
    }
}
