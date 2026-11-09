using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nimbus.Application.Features.FlightAssessment.Queries.AssessFlight;
using Nimbus.Contracts.DTOs.Features.FlightAssessment;
namespace Nimbus.API.Controllers;

/// <summary>
///     Assesses flight values (total weight, lift sufficiency, Go/No-Go verdict) with the signed-in user's own
///     thresholds. Reads and stores no flight data; the user comes from the auth context only.
/// </summary>
[ApiController]
[Route("api/flight-assessment")]
[Authorize]
public class FlightAssessmentController : ControllerBase
{
    private readonly ISender _mediator;

    public FlightAssessmentController(ISender mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    public async Task<ActionResult<FlightAssessmentDto>> Assess([FromBody] FlightAssessmentRequestDto request, CancellationToken ct)
    {
        return Ok(await _mediator.Send(new AssessFlightQuery(request), ct));
    }
}
