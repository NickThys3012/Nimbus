using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nimbus.Application.Features.Profile.Commands.SaveMyProfile;
using Nimbus.Application.Features.Profile.Queries.GetMyProfile;
using Nimbus.Contracts.DTOs.Features.Profile;
namespace Nimbus.API.Controllers;

/// <summary>
///     The signed-in user's own profile (operator name, PIC weight). There is no id in any route: the row is
///     always resolved from the authenticated user, so nobody else's profile can be addressed.
/// </summary>
[ApiController]
[Route("api/profile")]
[Authorize]
public class ProfileController : ControllerBase
{
    private readonly ISender _mediator;

    public ProfileController(ISender mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<ActionResult<ProfileDto>> Get(CancellationToken ct)
    {
        return Ok(await _mediator.Send(new GetMyProfileQuery(), ct));
    }

    [HttpPut]
    public async Task<ActionResult<ProfileDto>> Save(ProfileRequestDto request, CancellationToken ct)
    {
        return Ok(await _mediator.Send(new SaveMyProfileCommand(request), ct));
    }
}
