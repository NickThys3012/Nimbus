using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Nimbus.Infrastructure.Persistence;
namespace Nimbus.Infrastructure.Identity;

/// <summary>
///     Per-request check of a validated auth-cookie principal against the CURRENT state of the account
///     (one indexed lookup of the user plus its role names, a single round trip). A token is
///     rejected when the user no longer exists, is locked out/blocked, or its security stamp no
///     longer matches (stamp is rotated on role change, block, reject and delete). Roles come from
///     the database rather than the token, and an <c>nimbus:status</c> claim
///     (<c>active</c> / <c>pending</c> / <c>rejected</c>) is added for the approval requirement.
/// </summary>
public static class AccessTokenValidator
{
    // Identity's own stamp claim, written into the auth cookie by the SignInManager's claims factory.
    public const string SecurityStampClaim = "AspNet.Identity.SecurityStamp";
    public const string StatusClaim = "nimbus:status";
    public const string StatusActive = "active";
    public const string StatusPending = "pending";
    public const string StatusRejected = "rejected";

    /// <returns>A failure reason, or null when the token is still valid (the principal is then refreshed in place).</returns>
    public static async Task<string?> ValidateAsync(ClaimsPrincipal? principal, AppDbContext db, CancellationToken ct)
    {
        var userId = principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        var stamp = principal?.FindFirstValue(SecurityStampClaim);
        if (principal?.Identity is not ClaimsIdentity identity || userId is null || stamp is null)
        {
            return "Token does not identify an account state.";
        }

        var now = DateTimeOffset.UtcNow;
        var state = await db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new
            {
                u.SecurityStamp,
                u.LockoutEnd,
                u.IsApproved,
                u.IsRejected,
                Roles = db.UserRoles.Where(ur => ur.UserId == u.Id)
                    .Join(db.Roles, ur => ur.RoleId, r => r.Id, (_, r) => r.Name!)
                    .ToList()
            })
            .SingleOrDefaultAsync(ct);

        if (state is null || state.SecurityStamp != stamp || (state.LockoutEnd is not null && state.LockoutEnd > now))
        {
            return "Account no longer valid for this token.";
        }

        foreach (var claim in identity.FindAll(identity.RoleClaimType).ToList())
        {
            identity.RemoveClaim(claim);
        }
        foreach (var role in state.Roles)
        {
            identity.AddClaim(new Claim(identity.RoleClaimType, role));
        }

        var status = state.IsRejected ? StatusRejected : state.IsApproved ? StatusActive : StatusPending;
        identity.AddClaim(new Claim(StatusClaim, status));
        return null;
    }
}
