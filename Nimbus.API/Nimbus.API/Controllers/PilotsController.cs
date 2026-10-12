using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nimbus.Application.Features.Pilots.Commands.CreatePilot;
using Nimbus.Application.Features.Pilots.Commands.DeletePilot;
using Nimbus.Application.Features.Pilots.Commands.UpdatePilot;
using Nimbus.Application.Features.Pilots.Queries.GetPilot;
using Nimbus.Application.Features.Pilots.Queries.ListPilots;
using Nimbus.Contracts.DTOs.Features.Pilots;
namespace Nimbus.API.Controllers;

/// <summary>The signed-in user's own pilots. Another user's id answers 404, same as a missing one.</summary>
[ApiController]
[Route("api/pilots")]
[Authorize]
public class PilotsController : ControllerBase
{
    private readonly ISender _mediator;

    public PilotsController(ISender mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PilotDto>>> List(CancellationToken ct)
    {
        return Ok(await _mediator.Send(new ListPilotsQuery(), ct));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PilotDto>> Get(Guid id, CancellationToken ct)
    {
        return Ok(await _mediator.Send(new GetPilotQuery(id), ct));
    }

    [HttpPost]
    public async Task<ActionResult<PilotDto>> Create(PilotRequestDto request, CancellationToken ct)
    {
        var created = await _mediator.Send(new CreatePilotCommand(request), ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<PilotDto>> Update(Guid id, PilotRequestDto request, CancellationToken ct)
    {
        return Ok(await _mediator.Send(new UpdatePilotCommand(id, request), ct));
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new DeletePilotCommand(id), ct);
        return NoContent();
    }
}
