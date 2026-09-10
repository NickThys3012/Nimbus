using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
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

    [HttpPost("login")]
    public async Task<ActionResult<LoginResponseDto>> Login(LoginRequestDto request)
    {
        var user = await _users.FindByEmailAsync(request.Email);
        if (user is null)
        {
            return Unauthorized("Invalid credentials");
        }

        var signInResult = await _signInManager.CheckPasswordSignInAsync(user, request.Password, true);
        if (signInResult.IsNotAllowed)
        {
            return Unauthorized("Please verify your email before logging in.");
        }

        if (signInResult.IsLockedOut)
        {
            return Unauthorized("Account temporarily locked. Try again later.");
        }

        if (!signInResult.Succeeded)
        {
            return Unauthorized("Invalid credentials");
        }

        var roles = await _users.GetRolesAsync(user);
        var (accessToken, expiry) = _tokens.GenerateAccessToken(user, roles);
        var (rawRefresh, refreshEntity) = await _tokens.GenerateRefreshTokenAsync(user.Id, request.RememberMe);

        SetRefreshCookie(rawRefresh, request.RememberMe ? new DateTimeOffset(refreshEntity.ExpiresAt) : null);

        return Ok(new LoginResponseDto(accessToken, expiry, user.Email!, roles.FirstOrDefault() ?? nameof(UserRole.Pilot)));
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

        var existing = await _tokens.ValidateRefreshTokenAsync(raw);
        if (existing is null)
        {
            _logger.LogInformation(
                "Refresh rejected: refreshToken cookie did not match a valid, non-expired token (unknown, expired, or reuse of an already-rotated/revoked token).");
            return Unauthorized();
        }

        var user = await _users.FindByIdAsync(existing.UserId);
        if (user is null)
        {
            _logger.LogInformation("Refresh rejected: token was valid but its UserId {UserId} no longer exists.", existing.UserId);
            return Unauthorized();
        }

        var roles = await _users.GetRolesAsync(user);
        var (newAccess, expiry) = _tokens.GenerateAccessToken(user, roles);
        // Carry the "remember me" choice forward across rotation — it was fixed at login time
        // and shouldn't silently flip just because the token got rotated on this refresh.
        var (newRaw, newEntity) = await _tokens.GenerateRefreshTokenAsync(user.Id, existing.RememberMe);

        await _tokens.RevokeTokenAsync(existing, TokenService.HashToken(newRaw));

        SetRefreshCookie(newRaw, existing.RememberMe ? newEntity.ExpiresAt : null);

        return Ok(new LoginResponseDto(newAccess, expiry, user.Email!, roles.FirstOrDefault() ?? nameof(UserRole.Pilot)));
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
    [Authorize]
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
        return Ok(new CurrentUserDto(user.Id, user.Email!, user.FirstName, user.Name, roles.ToList(), user.IsApproved));
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
