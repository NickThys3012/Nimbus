using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nimbus.Application.Features.LiftCalculation.Queries.CalculateTotalLift;
using Nimbus.Contracts.DTOs.Features.LiftCalculation;
namespace Nimbus.API.Controllers;

/// <summary>Stateless total-lift calculation for the flight-preparation load section. Requires sign-in; reads and stores nothing.</summary>
[ApiController]
[Route("api/lift-calculation")]
[Authorize]
public class LiftCalculationController : ControllerBase
{
    private readonly ISender _mediator;

    public LiftCalculationController(ISender mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("total-lift")]
    public async Task<ActionResult<TotalLiftDto>> TotalLift(
        [FromQuery] decimal maxAltitudeMeters,
        [FromQuery] decimal siteElevationMeters,
        [FromQuery] decimal ambientTemperatureC,
        [FromQuery] decimal internalTemperatureC,
        [FromQuery] decimal envelopeVolumeM3,
        CancellationToken ct)
    {
        return Ok(await _mediator.Send(
            new CalculateTotalLiftQuery(maxAltitudeMeters, siteElevationMeters, ambientTemperatureC, internalTemperatureC, envelopeVolumeM3), ct));
    }
}
