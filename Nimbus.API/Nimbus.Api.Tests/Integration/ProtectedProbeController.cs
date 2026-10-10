using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Nimbus.Api.Tests.Integration;

/// <summary>Test-only endpoint behind the default [Authorize] policy (approved, not rejected).</summary>
[ApiController]
[Route("api/test/protected")]
[Authorize]
public class ProtectedProbeController : ControllerBase
{
    [HttpGet]
    public ActionResult Get()
    {
        return Ok();
    }
}
