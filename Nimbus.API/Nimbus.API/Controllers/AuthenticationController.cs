using System.Security.Claims;
using MediatR;
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
using Nimbus.Infrastructure.Identity;
namespace Nimbus.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthenticationController : ControllerBase
{
    private readonly ISender _mediator;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly TokenService _tokens;
    private readonly UserManager<ApplicationUser> _users;
    private readonly ILogger<AuthenticationController> _logger;
    private readonly IWebHostEnvironment _environment;

    public AuthenticationController(
        ISender mediator,
        TokenService tokens,
        UserManager<ApplicationUser> users,
        SignInManager<ApplicationUser> signInManager,
        ILogger<AuthenticationController> logger,
        IWebHostEnvironment environment)
    {
        _mediator = mediator;
        _tokens = tokens;
        _users = users;
        _signInManager = signInManager;
        _logger = logger;
        _environment = environment;
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

        // Tokens are minted under the session lock after re-reading the account, so a login racing an
        // admin block/delete cannot end up with a live session (see TokenService).
        var session = await _tokens.IssueSessionAsync(user.Id, user.SecurityStamp, request.RememberMe);
        if (session is null)
        {
            await RecordLoginAttemptAsync(user, user.Email, false, LoginFailureReason.LockedOut);
            return Unauthorized("Account temporarily locked. Try again later.");
        }

        SetRefreshCookie(session.RawRefresh, request.RememberMe ? new DateTimeOffset(session.RefreshEntity.ExpiresAt) : null);

        // Only now — access and refresh tokens issued and saved — is the sign-in a success.
        await RecordLoginAttemptAsync(user, user.Email, true);

        var fresh = session.User;
        return Ok(new LoginResponseDto(session.AccessToken, session.AccessExpiry, fresh.Email!, session.Roles.FirstOrDefault() ?? nameof(UserRole.Pilot), fresh.IsApproved, fresh.IsRejected, fresh.RequestedAt));
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

    // ── POST /api/auth/refresh ──────────────────────────────────────
    [HttpPost("refresh")]
    public async Task<ActionResult<LoginResponseDto>> Refresh()
    {
        var raw = Request.Cookies["refreshToken"];
        if (raw is null)
        {
            _logger.LogInformation("Refresh rejected: no refreshToken cookie on the request.");
            return Unauthorized();
        }

        // Rotation is atomic with respect to admin block/delete/role changes (see TokenService).
        var rotation = await _tokens.RotateRefreshTokenAsync(raw);
        if (rotation is null)
        {
            _logger.LogInformation(
                "Refresh rejected: unknown, expired or reused refresh token, or the account is locked out, rejected or deleted.");
            return Unauthorized();
        }

        var user = rotation.User;
        // Carry the "remember me" choice forward across rotation — it was fixed at login time.
        SetRefreshCookie(rotation.RawRefresh, rotation.RememberMe ? rotation.RefreshEntity.ExpiresAt : null);

        return Ok(new LoginResponseDto(rotation.AccessToken, rotation.AccessExpiry, user.Email!, rotation.Roles.FirstOrDefault() ?? nameof(UserRole.Pilot), user.IsApproved, user.IsRejected, user.RequestedAt));
    }

    // ── POST /api/auth/logout ───────────────────────────────────────
    [HttpPost("logout")]
    public async Task<ActionResult> Logout()
    {
        var raw = Request.Cookies["refreshToken"];
        if (raw is not null)
        {
            var token = await _tokens.ValidateRefreshTokenAsync(raw);
            if (token is not null)
            {
                await _tokens.RevokeTokenAsync(token);
            }
        }

        Response.Cookies.Delete("refreshToken");
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


    // ── Cookie helper ───────────────────────────────────────────────
    // `expires`: null means "session cookie" — the browser drops it when it closes, which is
    // the behaviour we want when the user did NOT check "remember me". A value means
    // persistent, surviving browser restarts (and matching the underlying refresh token's own
    // expiry exactly, so the cookie and the DB record it authenticates always agree).
    private void SetRefreshCookie(string raw, DateTimeOffset? expires)
    {
        // Secure=true requires the cookie to actually travel over HTTPS. In every real
        // deployment that's true (Caddy/VPS terminate TLS), but a plain `dotnet run`/
        // docker compose local dev loop (ASPNETCORE_ENVIRONMENT=Development) is typically
        // served over plain HTTP. Chrome/Firefox special-case `localhost` as a "secure
        // context" and still store/send Secure cookies there, but Safari does not — the
        // cookie is silently dropped by the browser, login "succeeds" (access token comes
        // back fine), and the very next /refresh call 401s because no refreshToken cookie
        // was ever actually stored. Only relax Secure in Development so production keeps
        // the hardened cookie.
        Response.Cookies.Append("refreshToken", raw, new CookieOptions
        {
            HttpOnly = true,
            Secure = !_environment.IsDevelopment(),
            SameSite = SameSiteMode.Strict,
            Expires = expires
        });
    }
}
