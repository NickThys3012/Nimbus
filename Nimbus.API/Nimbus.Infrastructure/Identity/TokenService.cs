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
    ///     Guarantee: no refresh token is minted (by login or by rotation) after an admin block, delete or
    ///     role change has revoked the account's sessions. Both paths run in a transaction under the
    ///     admin-set application lock taken Shared; those admin mutations take it Exclusive while they
    ///     revoke. A mint therefore either completes before the mutation starts (and is revoked by it) or
    ///     starts after it committed and sees the new account state. Refresh tokens are only ever created
    ///     through <see cref="IssueSessionAsync" /> and <see cref="RotateRefreshTokenAsync" />.
    /// </summary>
    private async Task<T?> UnderSessionLockAsync<T>(Func<Task<T?>> work, CancellationToken ct) where T : class
    {
        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            _db.ChangeTracker.Clear();
            await using var tx = await _db.Database.BeginTransactionAsync(ct);
            await AppLocks.AcquireAsync(_db, AppLocks.AdminSet, false, ct);
            var result = await work();
            await tx.CommitAsync(ct);
            return result;
        });
    }

    private static bool IsLockedOut(ApplicationUser user)
    {
        return user.LockoutEnd is not null && user.LockoutEnd > DateTimeOffset.UtcNow;
    }

    /// <summary>
    ///     Issues the access and refresh token for a successful password check. Inside the session lock it
    ///     re-reads the account and returns null when the account was deleted, locked out (blocked) or had
    ///     its security stamp changed (an admin mutation) since <paramref name="expectedSecurityStamp" /> was
    ///     read. Rejected accounts may still sign in: the SPA needs the tokens to show its notice.
    /// </summary>
    public Task<RotationResult?> IssueSessionAsync(
        string userId, string? expectedSecurityStamp, bool rememberMe, CancellationToken ct = default)
    {
        return UnderSessionLockAsync<RotationResult>(async () =>
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user is null || IsLockedOut(user) || user.SecurityStamp != expectedSecurityStamp)
            {
                return null;
            }

            var roles = await _userManager.GetRolesAsync(user);
            var (access, expiry) = GenerateAccessToken(user, roles);
            var (raw, entity) = await CreateRefreshTokenAsync(user.Id, rememberMe);
            return new RotationResult(user, roles, access, expiry, raw, entity, rememberMe);
        }, ct);
    }

    /// <summary>
    ///     Rotates a refresh token. Exactly one of several concurrent rotations of the same token can win:
    ///     the token is claimed with an atomic conditional update (<c>IsRevoked = 0 -> 1</c>, rows affected
    ///     must be 1) BEFORE anything is minted. A loser (or any replay of an already-rotated-out token) is
    ///     treated as reuse, exactly like the sequential case: it is refused and the user's whole token
    ///     family is revoked, so a double submit signs the user out just as a sequential replay already did.
    ///     Also refuses locked-out, rejected and deleted accounts. Returns null when the refresh is refused.
    /// </summary>
    public Task<RotationResult?> RotateRefreshTokenAsync(string raw, CancellationToken ct = default)
    {
        return UnderSessionLockAsync<RotationResult>(async () =>
        {
            var hash = HashToken(raw);
            var existing = await _db.RefreshTokens.AsNoTracking().FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
            if (existing is null)
            {
                return null;
            }

            if (existing.IsRevoked)
            {
                await RevokeAllUserTokensAsync(existing.UserId);
                return null;
            }

            var user = existing.ExpiresAt < DateTime.UtcNow ? null : await _userManager.FindByIdAsync(existing.UserId);
            if (user is null || user.IsRejected || IsLockedOut(user))
            {
                return null;
            }

            var (newRaw, newEntity) = BuildRefreshToken(user.Id, existing.RememberMe);
            var claimed = await _db.RefreshTokens
                .Where(t => t.Id == existing.Id && !t.IsRevoked)
                .ExecuteUpdateAsync(u => u
                    .SetProperty(t => t.IsRevoked, true)
                    .SetProperty(t => t.ReplacedByTokenHash, newEntity.TokenHash), ct);
            if (claimed != 1)
            {
                // Lost the race, or replay: reuse semantics.
                await RevokeAllUserTokensAsync(existing.UserId);
                return null;
            }

            _db.RefreshTokens.Add(newEntity);
            await _db.SaveChangesAsync(ct);

            var roles = await _userManager.GetRolesAsync(user);
            var (access, expiry) = GenerateAccessToken(user, roles);
            return new RotationResult(user, roles, access, expiry, newRaw, newEntity, existing.RememberMe);
        }, ct);
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
    // Private on purpose: refresh tokens may only be minted under the session lock (see
    // UnderSessionLockAsync), i.e. through IssueSessionAsync / RotateRefreshTokenAsync.
    private (string raw, RefreshToken entity) BuildRefreshToken(string userId, bool rememberMe)
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

        return (raw, entity);
    }

    private async Task<(string raw, RefreshToken entity)> CreateRefreshTokenAsync(string userId, bool rememberMe)
    {
        var (raw, entity) = BuildRefreshToken(userId, rememberMe);
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
