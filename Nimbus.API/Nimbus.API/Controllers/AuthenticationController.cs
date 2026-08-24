using MediatR;
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

    public AuthenticationController(
        ISender mediator,
        TokenService tokens,
        UserManager<ApplicationUser> users,
        SignInManager<ApplicationUser> signInManager)
    {
        _mediator = mediator;
        _tokens = tokens;
        _users = users;
        _signInManager = signInManager;
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
        var (rawRefresh, _) = await _tokens.GenerateRefreshTokenAsync(user.Id);

        SetRefreshCookie(rawRefresh);

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
            return Unauthorized();
        }

        var existing = await _tokens.ValidateRefreshTokenAsync(raw);
        if (existing is null)
        {
            return Unauthorized();
        }

        var user = await _users.FindByIdAsync(existing.UserId);
        if (user is null)
        {
            return Unauthorized();
        }

        var roles = await _users.GetRolesAsync(user);
        var (newAccess, expiry) = _tokens.GenerateAccessToken(user, roles);
        var (newRaw, _) = await _tokens.GenerateRefreshTokenAsync(user.Id);

        await _tokens.RevokeTokenAsync(existing, TokenService.HashToken(newRaw));

        SetRefreshCookie(newRaw);

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


    // ── Cookie helper ───────────────────────────────────────────────
    private void SetRefreshCookie(string raw)
    {
        Response.Cookies.Append("refreshToken", raw, new CookieOptions
        {
            HttpOnly = true, Secure = true, SameSite = SameSiteMode.Strict, Expires = DateTimeOffset.UtcNow.AddDays(7)
        });
    }
}
