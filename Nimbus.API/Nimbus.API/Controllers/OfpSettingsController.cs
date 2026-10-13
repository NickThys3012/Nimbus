using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nimbus.Application.Features.OfpSettings.Commands.ResetMyOfpSettings;
using Nimbus.Application.Features.OfpSettings.Commands.SaveMyOfpSettings;
using Nimbus.Application.Features.OfpSettings.Queries.GetMyOfpSettings;
using Nimbus.Contracts.DTOs.Features.OfpSettings;
namespace Nimbus.API.Controllers;

/// <summary>
///     The signed-in user's own OFP settings. There is no id in any route: the row is always
///     resolved from the authenticated user, so nobody else's row can be addressed.
/// </summary>
[ApiController]
[Route("api/ofp-settings")]
[Authorize]
public class OfpSettingsController : ControllerBase
{
    private readonly ISender _mediator;

    public OfpSettingsController(ISender mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<ActionResult<OfpSettingsDto>> Get(CancellationToken ct)
    {
        return Ok(await _mediator.Send(new GetMyOfpSettingsQuery(), ct));
    }

    [HttpPut]
    public async Task<ActionResult<OfpSettingsDto>> Save(OfpSettingsRequestDto request, CancellationToken ct)
    {
        return Ok(await _mediator.Send(new SaveMyOfpSettingsCommand(request), ct));
    }

    /// <summary>Reset to default: removes the caller's own row and returns the effective default.</summary>
    [HttpDelete]
    public async Task<ActionResult<OfpSettingsDto>> Reset(CancellationToken ct)
    {
        return Ok(await _mediator.Send(new ResetMyOfpSettingsCommand(), ct));
    }
}
