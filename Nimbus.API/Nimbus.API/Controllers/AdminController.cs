using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nimbus.Application.Features.Auth.Command.UnblockVerificationEmail;
using Nimbus.Contracts.DTOs.Features.Auth;
using Nimbus.Domain.Enums;
namespace Nimbus.API.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = nameof(UserRole.Admin))]
public class AdminController : ControllerBase
{
    private readonly ISender _mediator;

    public AdminController(ISender mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("verification-email/unblock")]
    public async Task<ActionResult> UnblockVerificationEmail(UnblockVerificationEmailRequestDto request, CancellationToken ct)
    {
        await _mediator.Send(new UnblockVerificationEmailCommand(request.Email), ct);
        return Ok();
    }
}
