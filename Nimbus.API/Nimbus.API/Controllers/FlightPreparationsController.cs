using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nimbus.Application.Features.FlightPreparations.Commands.CreateFlightPreparation;
using Nimbus.Application.Features.FlightPreparations.Commands.UpdateFlightPreparation;
using Nimbus.Application.Features.FlightPreparations.Queries.GetFlightPreparation;
using Nimbus.Application.Features.FlightPreparations.Queries.ListFlights;
using Nimbus.Contracts.DTOs.Features.FlightPreparations;
using Nimbus.Domain.Enums;
namespace Nimbus.API.Controllers;

/// <summary>
///     The signed-in user's flight overview: own flights plus those shared with them (an admin sees all).
///     The user comes from the auth context only; filtering, sorting and paging run in SQL.
/// </summary>
[ApiController]
[Route("api/flight-preparations")]
[Authorize]
public class FlightPreparationsController : ControllerBase
{
    private readonly ISender _mediator;

    public FlightPreparationsController(ISender mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<ActionResult<FlightListPageDto>> List(
        [FromQuery] FlightListFilter filter = FlightListFilter.All,
        [FromQuery] FlightListSort sort = FlightListSort.Date,
        [FromQuery] bool descending = true,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        return Ok(await _mediator.Send(new ListFlightsQuery(filter, sort, descending, page, pageSize), ct));
    }
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<FlightPreparationDto>> Get(Guid id, CancellationToken ct)
    {
        return Ok(await _mediator.Send(new GetFlightPreparationQuery(id), ct));
    }

    [HttpPost]
    public async Task<ActionResult<FlightPreparationDto>> Create(FlightPreparationRequestDto request, CancellationToken ct)
    {
        var created = await _mediator.Send(new CreateFlightPreparationCommand(request), ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<FlightPreparationDto>> Update(Guid id, FlightPreparationRequestDto request, CancellationToken ct)
    {
        return Ok(await _mediator.Send(new UpdateFlightPreparationCommand(id, request), ct));
    }
}
