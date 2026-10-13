using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nimbus.Application.Features.LaunchLocations.Commands.CreateLaunchLocation;
using Nimbus.Application.Features.LaunchLocations.Commands.DeleteLaunchLocation;
using Nimbus.Application.Features.LaunchLocations.Commands.UpdateLaunchLocation;
using Nimbus.Application.Features.LaunchLocations.Queries.GetLaunchLocation;
using Nimbus.Application.Features.LaunchLocations.Queries.ListLaunchLocations;
using Nimbus.Contracts.DTOs.Features.LaunchLocations;
namespace Nimbus.API.Controllers;

/// <summary>The signed-in user's own launch locations. Another user's id answers 404, same as a missing one.</summary>
[ApiController]
[Route("api/launch-locations")]
[Authorize]
public class LaunchLocationsController : ControllerBase
{
    private readonly ISender _mediator;

    public LaunchLocationsController(ISender mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<LaunchLocationDto>>> List(CancellationToken ct)
    {
        return Ok(await _mediator.Send(new ListLaunchLocationsQuery(), ct));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<LaunchLocationDto>> Get(Guid id, CancellationToken ct)
    {
        return Ok(await _mediator.Send(new GetLaunchLocationQuery(id), ct));
    }

    [HttpPost]
    public async Task<ActionResult<LaunchLocationDto>> Create(LaunchLocationRequestDto request, CancellationToken ct)
    {
        var created = await _mediator.Send(new CreateLaunchLocationCommand(request), ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<LaunchLocationDto>> Update(Guid id, LaunchLocationRequestDto request, CancellationToken ct)
    {
        return Ok(await _mediator.Send(new UpdateLaunchLocationCommand(id, request), ct));
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new DeleteLaunchLocationCommand(id), ct);
        return NoContent();
    }
}
