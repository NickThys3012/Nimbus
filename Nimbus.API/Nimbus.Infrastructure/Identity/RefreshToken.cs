namespace Nimbus.Infrastructure.Identity;

public class RefreshToken
{
    public int Id { get; set; }
    public string UserId { get; set; } = null!;
    public string TokenHash { get; set; } = null!; // SHA-256
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool IsRevoked { get; set; }
    public string? ReplacedByTokenHash { get; set; } // audit trail

    // "Remember me" (issue #15 follow-up): whether this token — and therefore the cookie
    // rotated from it — should persist across browser restarts (long expiry, persistent
    // cookie) or only for the current browser session (short expiry, session cookie, cleared
    // when the browser closes). Carried forward on every rotation so unchecking/checking the
    // box only takes effect on the next fresh login, not mid-session.
    public bool RememberMe { get; set; }

    // Navigation
    public ApplicationUser User { get; set; } = null!;
}
