using System.Net;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Nimbus.Application.Abstraction;
using Nimbus.Application.Common.Interfaces;
using Nimbus.Application.Features.Auth.Command.CreateUser;
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
    private readonly TokenService _tokens;
    private readonly UserManager<ApplicationUser> _users;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IEmailQueue _emailQueue;
    
    public AuthenticationController(
        ISender mediator,
        TokenService tokens,
        UserManager<ApplicationUser> users,
        SignInManager<ApplicationUser> signInManager,
        IEmailQueue emailQueue)
    {
        _mediator = mediator;
        _tokens = tokens;
        _users = users;
        _signInManager = signInManager;
        _emailQueue = emailQueue;
    }
    
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponseDto>> Login(LoginRequestDto request)
    {
        var user = await _users.FindByEmailAsync(request.Email);
        if (user is null)
        {
            return Unauthorized("Invalid credentials");
        }

        var signInResult = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
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

        return Ok(new LoginResponseDto(accessToken, expiry, user.Email!, roles.FirstOrDefault()?? nameof(UserRole.Pilot)));
    }

    [HttpPost("register")]
    public async Task<ActionResult> Register(RegisterRequestDto request, CancellationToken ct)
    {
        var userId = await _mediator.Send(new CreateUserCommand(request), ct);
        var user = await _users.FindByIdAsync(userId);
        if (user is null)
        {
            return Problem(statusCode: StatusCodes.Status500InternalServerError, detail: "User was created but could not be loaded.");
        }

        await SendEmailVerificationAsync(user, ct);
        return Created();
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

    private async Task SendEmailVerificationAsync(ApplicationUser user, CancellationToken ct)
    {
        var token = await _users.GenerateEmailConfirmationTokenAsync(user);
        var confirmUrl = Url.ActionLink(
            action: nameof(ConfirmEmail),
            controller: "Authentication",
            values: new { userId = user.Id, token },
            protocol: Request.Scheme);

        if (confirmUrl is null)
        {
            throw new InvalidOperationException("Could not generate email verification link.");
        }

        var firstName = string.IsNullOrWhiteSpace(user.FirstName) ? "there" : user.FirstName;
        var safeFirstName = WebUtility.HtmlEncode(firstName);

        await _emailQueue.EnqueueAsync(new EmailMessage
        {
            ToAddress = user.Email!,
            ToName = firstName,
            Subject = "Verify your Nimbus email address",
            Template = "email-confirmation",
            TextBody =
                $"Hello {firstName},\n\nThanks for registering with Nimbus.\nPlease verify your email by visiting this link:\n{confirmUrl}\n\nIf you did not create this account, you can ignore this email.",
            HtmlBody =
                $"<p>Hello {safeFirstName},</p><p>Thanks for registering with Nimbus.</p><p>Please verify your email by clicking <a href=\"{confirmUrl}\">this link</a>.</p><p>If you did not create this account, you can ignore this email.</p>"
        }, ct);
    }
}
