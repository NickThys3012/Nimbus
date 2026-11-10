using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nimbus.Application.Features.Solar.Queries.GetSunTimes;
using Nimbus.Contracts.DTOs.Features.Solar;
namespace Nimbus.API.Controllers;

/// <summary>Sunrise and sunset by the NOAA solar-position algorithm. Requires sign-in; calculates only, reads and stores nothing.</summary>
[ApiController]
[Route("api/solar")]
[Authorize]
public class SolarController : ControllerBase
{
    private readonly ISender _mediator;

    public SolarController(ISender mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    ///     Sunrise, solar noon and sunset for a place on a local calendar date (<c>yyyy-MM-dd</c>), as UTC instants, plus <c>HH:mm</c>
    ///     in <c>timeZone</c> (an IANA id) when given. 422 for out-of-range coordinates, a bad date or an unknown zone.
    /// </summary>
    [HttpGet("sun-times")]
    public async Task<ActionResult<SunTimesDto>> SunTimes(
        [FromQuery] double latitude, [FromQuery] double longitude, [FromQuery] DateOnly date, [FromQuery] string? timeZone, CancellationToken ct)
    {
        return Ok(await _mediator.Send(new GetSunTimesQuery(latitude, longitude, date, timeZone), ct));
    }
}
