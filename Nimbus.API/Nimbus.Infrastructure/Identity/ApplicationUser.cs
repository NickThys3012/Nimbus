using Microsoft.AspNetCore.Identity;
using Nimbus.Domain.Enums;
namespace Nimbus.Infrastructure.Identity;

public class ApplicationUser : IdentityUser
{
    // Role stored here for Identity purposes; Domain.User.Role mirrors it
    public UserRole Role { get; set; } = UserRole.Pilot; // UserRole enum from Domain — safe to reference

    public string FirstName { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public bool IsApproved { get; set; }

    public bool IsRejected { get; set; }

    /// <summary>When the account was requested (registered), UTC.</summary>
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

    public DateTimeOffset? VerificationEmailCooldownEnd { get; set; }

    public int VerificationEmailSendCount { get; set; }

    public bool VerificationEmailLocked { get; set; }
}
