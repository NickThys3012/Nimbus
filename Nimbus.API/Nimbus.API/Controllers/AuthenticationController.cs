using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Nimbus.Application.Features.Admin.Commands.RecordLoginAttempt;
using Nimbus.Application.Features.Auth.Command.CreateUser;
using Nimbus.Application.Features.Auth.Command.ResendVerificationEmail;
using Nimbus.Application.Features.Auth.Queries.GetUserByEmail;
using Nimbus.Contracts.DTOs.Features.Auth;
using Nimbus.Contracts.DTOs.Features.Auth.Register;
using Nimbus.Domain.Enums;
using Nimbus.API.Security;
using Nimbus.Infrastructure.Identity;
namespace Nimbus.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthenticationController : ControllerBase
{
    private readonly ISender _mediator;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _users;
    private readonly ILogger<AuthenticationController> _logger;
    private readonly XsrfTokenWriter _xsrf;
    private readonly IConfiguration _config;

    public AuthenticationController(
        ISender mediator,
        UserManager<ApplicationUser> users,
        SignInManager<ApplicationUser> signInManager,
        ILogger<AuthenticationController> logger,
        XsrfTokenWriter xsrf,
        IConfiguration config)
    {
        _mediator = mediator;
        _users = users;
        _signInManager = signInManager;
        _logger = logger;
        _xsrf = xsrf;
        _config = config;
    }

    [EnableRateLimiting(AuthorizationPolicies.LoginRateLimit)]
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponseDto>> Login(LoginRequestDto request)
    {
        var user = await _users.FindByEmailAsync(request.Email);
        if (user is null)
        {
            await RecordLoginAttemptAsync(null, null, false, LoginFailureReason.InvalidPassword);
            return Unauthorized("Invalid credentials");
        }

        var signInResult = await _signInManager.CheckPasswordSignInAsync(user, request.Password, true);
        if (signInResult.IsNotAllowed)
        {
            await RecordLoginAttemptAsync(user, user.Email, false, LoginFailureReason.NotApproved);
            return Unauthorized("Please verify your email before logging in.");
        }

        if (signInResult.IsLockedOut)
        {
            await RecordLoginAttemptAsync(user, user.Email, false, LoginFailureReason.LockedOut);
            return Unauthorized("Account temporarily locked. Try again later.");
        }

        if (!signInResult.Succeeded)
        {
            await RecordLoginAttemptAsync(user, user.Email, false, LoginFailureReason.InvalidPassword);
            return Unauthorized("Invalid credentials");
        }

        // The principal is built from the account as it is now. A block/delete/role change that races this
        // login has rotated the security stamp (or locked the account), so the cookie is refused on its
        // very next request (see AccessTokenValidator) — no session can outlive an admin action.
        var principal = await _signInManager.CreateUserPrincipalAsync(user);
        var properties = new AuthenticationProperties { IsPersistent = request.RememberMe };
        if (request.RememberMe)
        {
            properties.ExpiresUtc = DateTimeOffset.UtcNow.AddDays(_config.GetValue("Auth:RememberMeDays", 7));
        }

        await HttpContext.SignInAsync(IdentityConstants.ApplicationScheme, principal, properties);

        // Only now — the session cookie issued — is the sign-in a success.
        await RecordLoginAttemptAsync(user, user.Email, true);

        // Antiforgery tokens are bound to the identity: hand out one for the signed-in user.
        HttpContext.User = principal;
        _xsrf.Issue(HttpContext);

        var roles = await _users.GetRolesAsync(user);
        return Ok(new LoginResponseDto(user.Email!, roles.FirstOrDefault() ?? nameof(UserRole.Pilot), user.IsApproved, user.IsRejected, user.RequestedAt));
    }

    // Audit trail for the admin "Sign-in history" tab. Only enqueues; LoginEventWriterService persists it, so a slow or failing write never touches the login.
    private async Task RecordLoginAttemptAsync(ApplicationUser? user, string? email, bool succeeded, LoginFailureReason? failureReason = null)
    {
        try
        {
            await _mediator.Send(new RecordLoginAttemptCommand(
                user is null ? null : Guid.Parse(user.Id), email, succeeded, HttpContext.Connection.RemoteIpAddress?.ToString(), failureReason));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not record login attempt for the audit list.");
        }
    }

    [HttpPost("register")]
    public async Task<ActionResult> Register(RegisterRequestDto request, CancellationToken ct)
    {
        await _mediator.Send(new CreateUserCommand(request, $"{Request.Scheme}://{Request.Host}"), ct);
        return Created();
    }

    [HttpPost("resend-verification-email")]
    public async Task<ActionResult> ResendVerificationEmail(ResendVerificationEmailCommandDto request, CancellationToken ct)
    {
        await _mediator.Send(new ResendVerificationEmailCommand(request, $"{Request.Scheme}://{Request.Host}"), ct);
        return Ok();
    }

    [HttpGet("confirm-email")]
    public async Task<ActionResult> ConfirmEmail([FromQuery] string userId, [FromQuery] string token)
    {
        const string handoffPath = "/email-verification";

        var user = await _users.FindByIdAsync(userId);
        if (user is null)
        {
            return Redirect($"{handoffPath}?status=error");
        }

        var result = await _users.ConfirmEmailAsync(user, token);
        return Redirect(!result.Succeeded ? $"{handoffPath}?status=error" : $"{handoffPath}?status=success");
    }

    // ── POST /api/auth/logout ───────────────────────────────────────
    [HttpPost("logout")]
    public async Task<ActionResult> Logout()
    {
        await HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);

        // Signed out: swap the token for the anonymous identity's, so the next login POST still validates.
        HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity());
        _xsrf.Issue(HttpContext);
        return Ok();
    }

    [HttpGet]
    public async Task<ActionResult<UserDto>> Get([FromQuery] string email)
    {
        var user = await _mediator.Send(new GetUserByEmailQuery(email));
        return Ok(user);
    }

    // ── GET /api/authentication/me ───────────────────────────────────
    // Reports identity, roles and approval state for the signed-in user so the Angular shell
    // can decide what to render (issue #15) — separate from login/refresh so the app can
    // re-check approval state at any point during an active session, not only right after
    // authenticating.
    [Authorize(Policy = AuthorizationPolicies.Authenticated)]
    [HttpGet("me")]
    public async Task<ActionResult<CurrentUserDto>> Me()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var user = userId is null ? null : await _users.FindByIdAsync(userId);
        if (user is null)
        {
            return Unauthorized();
        }

        var roles = await _users.GetRolesAsync(user);
        return Ok(new CurrentUserDto(user.Id, user.Email!, user.FirstName, user.Name, roles.ToList(), user.IsApproved, user.IsRejected, user.RequestedAt));
    }
}
