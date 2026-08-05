using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Nimbus.Application.Features.Auth.Queries.GetUserByEmail;
using Nimbus.Contracts.DTOs.Features.Auth;
using Nimbus.Domain.Enums;
using Nimbus.Infrastructure.Identity;
namespace Nimbus.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthenticationController : ControllerBase
{
    private readonly ISender _mediator;
    private readonly TokenService _tokens;

    private readonly UserManager<ApplicationUser> _users;
    
    public AuthenticationController(ISender mediator, TokenService tokens, UserManager<ApplicationUser> users)
    {
        _mediator = mediator;
        _tokens = tokens;
        _users = users;
    }
    
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequestDto request)
    {
        var user = await _users.FindByEmailAsync(request.Email);
        if (user is null)
        {
            return Unauthorized("Invalid credentials");
        }

        var valid = await _users.CheckPasswordAsync(user, request.Password);
        if (!valid)
        {
            return Unauthorized("Invalid credentials");
        }

        var roles = await _users.GetRolesAsync(user);
        var (accessToken, expiry) = _tokens.GenerateAccessToken(user, roles);
        var (rawRefresh, _) = await _tokens.GenerateRefreshTokenAsync(user.Id);

        SetRefreshCookie(rawRefresh);

        return Ok(new LoginResponseDto(accessToken, expiry, user.Email!, roles.FirstOrDefault()?? nameof(UserRole.Pilot)));
    }
    
    // ── POST /api/auth/refresh ──────────────────────────────────────
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh()
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
    public async Task<IActionResult> Logout()
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
    public async Task<IActionResult> Get([FromQuery] string email)
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
