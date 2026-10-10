using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nimbus.Application.Features.Admin.Commands.ApproveUser;
using Nimbus.Application.Features.Admin.Commands.DeleteUser;
using Nimbus.Application.Features.Admin.Commands.RejectUser;
using Nimbus.Application.Features.Admin.Commands.SetUserBlocked;
using Nimbus.Application.Features.Admin.Commands.SetUserRole;
using Nimbus.Application.Features.Admin.Queries.ListLoginEvents;
using Nimbus.Application.Features.Admin.Queries.ListUsers;
using Nimbus.Application.Features.Auth.Command.UnblockVerificationEmail;
using Nimbus.Contracts.DTOs.Features.Admin;
using Nimbus.Contracts.DTOs.Features.Auth;
namespace Nimbus.API.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
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

    [HttpGet("users")]
    public async Task<ActionResult<IReadOnlyList<AdminUserDto>>> ListUsers(CancellationToken ct)
    {
        return Ok(await _mediator.Send(new ListUsersQuery(), ct));
    }

    [HttpPost("users/{id:guid}/approve")]
    public async Task<ActionResult> ApproveUser(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new ApproveUserCommand(id), ct);
        return NoContent();
    }

    [HttpPost("users/{id:guid}/reject")]
    public async Task<ActionResult> RejectUser(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new RejectUserCommand(id), ct);
        return NoContent();
    }

    [HttpPut("users/{id:guid}/role")]
    public async Task<ActionResult> SetUserRole(Guid id, SetUserRoleRequestDto request, CancellationToken ct)
    {
        await _mediator.Send(new SetUserRoleCommand(id, request.Role), ct);
        return NoContent();
    }

    [HttpPost("users/{id:guid}/block")]
    public async Task<ActionResult> BlockUser(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new SetUserBlockedCommand(id, true), ct);
        return NoContent();
    }

    [HttpPost("users/{id:guid}/unblock")]
    public async Task<ActionResult> UnblockUser(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new SetUserBlockedCommand(id, false), ct);
        return NoContent();
    }

    [HttpDelete("users/{id:guid}")]
    public async Task<ActionResult> DeleteUser(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new DeleteUserCommand(id), ct);
        return NoContent();
    }

    [HttpGet("login-events")]
    public async Task<ActionResult<IReadOnlyList<LoginEventDto>>> ListLoginEvents(
        [FromQuery] int limit = ListLoginEventsQuery.DefaultLimit, CancellationToken ct = default)
    {
        return Ok(await _mediator.Send(new ListLoginEventsQuery(limit), ct));
    }
}
