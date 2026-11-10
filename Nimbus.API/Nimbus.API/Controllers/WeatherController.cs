using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nimbus.Application.Features.Weather.Queries.GetMetarTaf;
using Nimbus.Application.Features.Weather.Queries.GetWindProfile;
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

    /// <summary>
    ///     Wind by altitude (low levels plus 950/925/900/850 hPa) for a place and UTC hour, lowest first. 422 when the model is
    ///     not supported or lacks a required level, 502 when the provider is down. Nothing is stored.
    /// </summary>
    [HttpGet("wind-profile")]
    public async Task<ActionResult<WindProfileDto>> WindProfile(
        [FromQuery] double latitude, [FromQuery] double longitude, [FromQuery] DateTime? hourUtc, [FromQuery] string? model, CancellationToken ct)
    {
        return Ok(await _mediator.Send(new GetWindProfileQuery(latitude, longitude, hourUtc, model), ct));
    }
}
