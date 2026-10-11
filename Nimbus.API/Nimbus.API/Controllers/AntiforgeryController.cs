using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nimbus.API.Security;
namespace Nimbus.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
public class AntiforgeryController : ControllerBase
{
    private readonly XsrfTokenWriter _xsrf;

    public AntiforgeryController(XsrfTokenWriter xsrf)
    {
        _xsrf = xsrf;
    }

    // The SPA calls this once at startup so its first login/register POST already carries a token.
    [HttpGet]
    public IActionResult Get()
    {
        _xsrf.Issue(HttpContext);
        return NoContent();
    }
}
