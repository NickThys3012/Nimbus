using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Nimbus.Infrastructure.Persistence;
namespace Nimbus.Infrastructure.Identity;

public class TokenService
{
    private readonly IConfiguration _config;
    private readonly AppDbContext _db;

    private readonly UserManager<ApplicationUser> _userManager;

    public TokenService(IConfiguration config, AppDbContext db, UserManager<ApplicationUser> userManager)
    {
        _config = config;
        _db = db;
        _userManager = userManager;
    }

    public sealed record RotationResult(
        ApplicationUser User, IList<string> Roles, string AccessToken, DateTime AccessExpiry,
        string RawRefresh, RefreshToken RefreshEntity, bool RememberMe);

    /// <summary>
    ///     Rotates a refresh token atomically: under a Shared application lock (an admin block/delete/role
    ///     change holds it Exclusive while it revokes sessions) it re-reads the account, refuses locked-out,
    ///     rejected or deleted accounts, and only then mints the replacement and revokes the old token. No
    ///     token can therefore be minted after a block's revoke point. Returns null when the refresh must
    ///     be rejected (unknown/expired/revoked token or an account that may no longer refresh).
    /// </summary>
    public async Task<RotationResult?> RotateRefreshTokenAsync(string raw, CancellationToken ct = default)
    {
        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            _db.ChangeTracker.Clear();
            await using var tx = await _db.Database.BeginTransactionAsync(ct);
            await AppLocks.AcquireAsync(_db, AppLocks.AdminSet, false, ct);

            // Includes reuse detection (revokes the family); committed even when we return null.
            var existing = await ValidateRefreshTokenAsync(raw);
            var user = existing is null ? null : await _userManager.FindByIdAsync(existing.UserId);
            if (existing is null || user is null || user.IsRejected
                || (user.LockoutEnd is not null && user.LockoutEnd > DateTimeOffset.UtcNow))
            {
                await tx.CommitAsync(ct);
                return null;
            }

            var roles = await _userManager.GetRolesAsync(user);
            var (access, expiry) = GenerateAccessToken(user, roles);
            var (newRaw, newEntity) = await GenerateRefreshTokenAsync(user.Id, existing.RememberMe);
            await RevokeTokenAsync(existing, HashToken(newRaw));
            await tx.CommitAsync(ct);
            return new RotationResult(user, roles, access, expiry, newRaw, newEntity, existing.RememberMe);
        });
    }

    // ── Access token ────────────────────────────────────────────────
    public (string token, DateTime expiry) GenerateAccessToken(
        ApplicationUser user,
        IList<string> roles)
    {
        var expiry = DateTime.UtcNow.AddMinutes(
            _config.GetValue("Jwt:AccessTokenMinutes", 15));

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id), new(ClaimTypes.Name, user.Name), new(ClaimTypes.Email, user.Email!), new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            // Checked on every request (AccessTokenValidator): a role change, block, reject or
            // delete rotates the stamp, which invalidates every token issued before it.
            new(AccessTokenValidator.SecurityStampClaim, user.SecurityStamp ?? string.Empty)
        };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_config["Jwt:Secret"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var jwt = new JwtSecurityToken(
            _config["Jwt:Issuer"],
            _config["Jwt:Audience"],
            claims,
            expires: expiry,
            signingCredentials: creds);

        return (new JwtSecurityTokenHandler().WriteToken(jwt), expiry);
    }

    // ── Refresh token ────────────────────────────────────────────────
    public async Task<(string raw, RefreshToken entity)>
        GenerateRefreshTokenAsync(string userId, bool rememberMe = false)
    {
        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        var hash = HashToken(raw);

        // "Remember me": a long-lived token when checked (matches the persistent cookie),
        // a short-lived one otherwise so an unattended shared machine isn't left with a
        // week-long valid session cookie after the browser is closed and reopened.
        var expiresAt = rememberMe
            ? DateTime.UtcNow.AddDays(_config.GetValue("Jwt:RefreshTokenDays", 7))
            : DateTime.UtcNow.AddHours(_config.GetValue("Jwt:RefreshTokenSessionHours", 12));

        var entity = new RefreshToken
        {
            UserId = userId, TokenHash = hash, CreatedAt = DateTime.UtcNow, ExpiresAt = expiresAt, RememberMe = rememberMe
        };

        _db.RefreshTokens.Add(entity);
        await _db.SaveChangesAsync();
        return (raw, entity);
    }

    // ── Validate (includes reuse detection) ─────────────────────────
    public async Task<RefreshToken?> ValidateRefreshTokenAsync(string raw)
    {
        var hash = HashToken(raw);
        var token = await _db.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == hash);

        if (token is null)
        {
            return null;
        }

        // Reuse detection: revoked token presented → revoke entire family
        if (!token.IsRevoked)
        {
            return token.ExpiresAt < DateTime.UtcNow ? null : token;
        }

        await RevokeAllUserTokensAsync(token.UserId);
        return null;
    }

    public async Task RevokeTokenAsync(
        RefreshToken token,
        string? replacedByHash = null)
    {
        token.IsRevoked = true;
        token.ReplacedByTokenHash = replacedByHash;
        await _db.SaveChangesAsync();
    }

    private async Task RevokeAllUserTokensAsync(string userId)
    {
        var tokens = await _db.RefreshTokens
            .Where(t => t.UserId == userId && !t.IsRevoked)
            .ToListAsync();
        tokens.ForEach(t => t.IsRevoked = true);
        await _db.SaveChangesAsync();
    }

    public static string HashToken(string raw)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToBase64String(bytes);
    }
}
