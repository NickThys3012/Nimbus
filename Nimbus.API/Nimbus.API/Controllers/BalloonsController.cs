using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nimbus.Application.Features.Balloons.Commands.CreateBalloon;
using Nimbus.Application.Features.Balloons.Commands.DeleteBalloon;
using Nimbus.Application.Features.Balloons.Commands.UpdateBalloon;
using Nimbus.Application.Features.Balloons.Queries.GetBalloon;
using Nimbus.Application.Features.Balloons.Queries.GetBalloonUsage;
using Nimbus.Application.Features.Balloons.Queries.ListBalloons;
using Nimbus.Contracts.DTOs.Features.Balloons;
namespace Nimbus.API.Controllers;

/// <summary>The signed-in pilot's own balloons. Another user's id answers 404, same as a missing one.</summary>
[ApiController]
[Route("api/balloons")]
[Authorize]
public class BalloonsController : ControllerBase
{
    private readonly ISender _mediator;

    public BalloonsController(ISender mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<BalloonDto>>> List(CancellationToken ct)
    {
        return Ok(await _mediator.Send(new ListBalloonsQuery(), ct));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<BalloonDto>> Get(Guid id, CancellationToken ct)
    {
        return Ok(await _mediator.Send(new GetBalloonQuery(id), ct));
    }

    /// <summary>The flights using this balloon; a used balloon cannot be deleted (409).</summary>
    [HttpGet("{id:guid}/usage")]
    public async Task<ActionResult<BalloonUsageDto>> Usage(Guid id, CancellationToken ct)
    {
        return Ok(await _mediator.Send(new GetBalloonUsageQuery(id), ct));
    }

    [HttpPost]
    public async Task<ActionResult<BalloonDto>> Create(BalloonRequestDto request, CancellationToken ct)
    {
        var created = await _mediator.Send(new CreateBalloonCommand(request), ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<BalloonDto>> Update(Guid id, BalloonRequestDto request, CancellationToken ct)
    {
        return Ok(await _mediator.Send(new UpdateBalloonCommand(id, request), ct));
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new DeleteBalloonCommand(id), ct);
        return NoContent();
    }
}
