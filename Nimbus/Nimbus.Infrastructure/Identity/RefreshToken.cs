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

    // Navigation
    public ApplicationUser User { get; set; } = null!;
}
