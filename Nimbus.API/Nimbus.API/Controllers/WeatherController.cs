using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nimbus.Application.Features.Weather.Queries.GetHourlyForecast;
using Nimbus.Application.Features.Weather.Queries.GetMetarTaf;
using Nimbus.Application.Features.Weather.Queries.GetWindProfile;
using Nimbus.Application.Features.Weather.Queries.GetWindSeries;
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

    /// <summary>
    ///     Hourly forecast (temperature, wind, gusts, CAPE, precipitation) and sunrise/sunset for a place over the configured
    ///     horizon, in the site's own timezone. 422 for an unsupported model or bad coordinates, 502 when the provider is down.
    ///     Nothing is stored.
    /// </summary>
    [HttpGet("hourly-forecast")]
    public async Task<ActionResult<HourlyForecastDto>> HourlyForecast(
        [FromQuery] double latitude, [FromQuery] double longitude, [FromQuery] string? model, CancellationToken ct)
    {
        return Ok(await _mediator.Send(new GetHourlyForecastQuery(latitude, longitude, model), ct));
    }

    /// <summary>
    ///     Hourly wind snapshots across the pressure levels and the 10-200 m levels between two UTC hours, for the trajectory
    ///     simulator. The window is clamped to what the model offers and the shortfall is reported; a provider outage is an empty
    ///     series with <c>providerAvailable: false</c>, not an error. 422 for an unsupported model or a bad window. Nothing is stored.
    /// </summary>
    [HttpGet("wind-series")]
    public async Task<ActionResult<WindSeriesDto>> WindSeries(
        [FromQuery] double latitude, [FromQuery] double longitude, [FromQuery] DateTime fromUtc, [FromQuery] DateTime toUtc, [FromQuery] string? model, CancellationToken ct)
    {
        return Ok(await _mediator.Send(new GetWindSeriesQuery(latitude, longitude, fromUtc, toUtc, model), ct));
    }
}
