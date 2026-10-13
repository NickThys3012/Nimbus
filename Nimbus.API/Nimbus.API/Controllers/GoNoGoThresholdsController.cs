using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nimbus.Application.Features.GoNoGoThresholds.Commands.ResetMyGoNoGoThresholds;
using Nimbus.Application.Features.GoNoGoThresholds.Commands.SaveMyGoNoGoThresholds;
using Nimbus.Application.Features.GoNoGoThresholds.Queries.GetMyGoNoGoThresholds;
using Nimbus.Contracts.DTOs.Features.GoNoGoThresholds;
namespace Nimbus.API.Controllers;

/// <summary>
///     The signed-in user's own Go/No-Go thresholds. There is no id in any route: the row is always
///     resolved from the authenticated user, so nobody else's row can be addressed.
/// </summary>
[ApiController]
[Route("api/go-no-go-thresholds")]
[Authorize]
public class GoNoGoThresholdsController : ControllerBase
{
    private readonly ISender _mediator;

    public GoNoGoThresholdsController(ISender mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<ActionResult<GoNoGoThresholdsDto>> Get(CancellationToken ct)
    {
        return Ok(await _mediator.Send(new GetMyGoNoGoThresholdsQuery(), ct));
    }

    [HttpPut]
    public async Task<ActionResult<GoNoGoThresholdsDto>> Save(GoNoGoThresholdsRequestDto request, CancellationToken ct)
    {
        return Ok(await _mediator.Send(new SaveMyGoNoGoThresholdsCommand(request), ct));
    }

    /// <summary>Reset to defaults: removes the caller's own row and returns the effective defaults.</summary>
    [HttpDelete]
    public async Task<ActionResult<GoNoGoThresholdsDto>> Reset(CancellationToken ct)
    {
        return Ok(await _mediator.Send(new ResetMyGoNoGoThresholdsCommand(), ct));
    }
}
