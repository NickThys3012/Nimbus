using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nimbus.Contracts.DTOs.Features.Telemetry;
namespace Nimbus.API.Controllers;

/// <summary>
/// Receives page views and unhandled errors from the Angular client (issue #12)
/// and logs them through the same Serilog/Loki pipeline as the API's own logs, so
/// a client-side crash shows up next to the backend logs for the same request.
/// Anonymous: the whole point is to catch errors that happen before/around login.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
public class TelemetryController : ControllerBase
{
    private readonly ILogger<TelemetryController> _logger;

    public TelemetryController(ILogger<TelemetryController> logger)
    {
        _logger = logger;
    }

    // ── POST /api/telemetry ─────────────────────────────────────────
    [HttpPost]
    [RequestSizeLimit(32 * 1024)]
    public IActionResult Post([FromBody] ClientTelemetryEventDto[]? events)
    {
        if (events is null || events.Length == 0)
        {
            return NoContent();
        }

        // Capped so one misbehaving client cannot use this endpoint to flood Loki.
        foreach (var evt in events.Take(50))
        {
            var url = (evt.Url ?? string.Empty).Split('?', '#')[0];
            var message = evt.Message is { Length: > 1024 } ? evt.Message[..1024] + "…" : evt.Message;
            var stack = evt.Stack is { Length: > 4096 } ? evt.Stack[..4096] + "…" : evt.Stack;

            using (_logger.BeginScope(new Dictionary<string, object?>
            {
                ["ClientEventType"] = evt.Type.ToString(),
                ["ClientUrl"] = url
            }))
            {
                if (evt.Type == ClientTelemetryEventType.UnhandledError)
                {
                    _logger.LogError(
                        "Client unhandled error on {ClientUrl}: {ClientMessage}\n{ClientStack}",
                        url, message, stack);
                }
                else
                {
                    _logger.LogInformation("Client page view: {ClientUrl}", evt.Url);
                }
            }
        }

        return NoContent();
    }
}
