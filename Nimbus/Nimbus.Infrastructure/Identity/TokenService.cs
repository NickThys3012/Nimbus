using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Nimbus.Infrastructure.Persistence;
namespace Nimbus.Infrastructure.Identity;

public class TokenService
{
    private readonly IConfiguration _config;
    private readonly AppDbContext _db;

    public TokenService(IConfiguration config, AppDbContext db)
    {
        _config = config;
        _db = db;
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
            new(ClaimTypes.NameIdentifier, user.Id), new(ClaimTypes.Name, user.Name), new(ClaimTypes.Email, user.Email!), new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
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
        GenerateRefreshTokenAsync(string userId)
    {
        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        var hash = HashToken(raw);
        var days = _config.GetValue("Jwt:RefreshTokenDays", 7);

        var entity = new RefreshToken
        {
            UserId = userId, TokenHash = hash, CreatedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow.AddDays(days)
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
