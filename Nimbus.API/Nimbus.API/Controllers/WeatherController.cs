using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nimbus.Application.Features.Weather.Queries.GetMetarTaf;
using Nimbus.Contracts.DTOs.Features.Weather;
namespace Nimbus.API.Controllers;

/// <summary>Weather data from outside providers. Nothing here is stored: a failed fetch changes nothing.</summary>
[ApiController]
[Route("api/weather")]
[Authorize]
public class WeatherController : ControllerBase
{
    private readonly ISender _mediator;

    public WeatherController(ISender mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    ///     The current METAR and TAF for an ICAO station, as raw text. 422 for a malformed code, 502 when the provider is down,
    ///     200 with nulls when the station publishes nothing.
    /// </summary>
    [HttpGet("metar-taf")]
    public async Task<ActionResult<MetarTafDto>> MetarTaf([FromQuery] string? icao, CancellationToken ct)
    {
        return Ok(await _mediator.Send(new GetMetarTafQuery(icao ?? ""), ct));
    }
}
