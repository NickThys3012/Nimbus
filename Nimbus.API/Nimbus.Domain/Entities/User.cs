using Nimbus.Domain.Entities.Base;
using Nimbus.Domain.Enums;
namespace Nimbus.Domain.Entities;

//extraction of the user entity from the identity package so that the identity stuff is not in the domain layer
public class User : BaseEntity
{
    public User(
        Guid id,
        string email,
        string name,
        string firstName,
        UserRole role,
        bool emailConfirmed,
        DateTimeOffset? verificationEmailCooldownEndUtc = null,
        int verificationEmailSendCount = 0,
        bool verificationEmailLocked = false)
    {
        Id = id;
        Email = email;
        Name = name;
        FirstName = firstName;
        EmailConfirmed = emailConfirmed;
        Role = role;
        VerificationEmailCooldownEndUtc = verificationEmailCooldownEndUtc;
        VerificationEmailSendCount = verificationEmailSendCount;
        VerificationEmailLocked = verificationEmailLocked;
    }
    public string Email { get; private set; }
    public string Name { get; private set; }
    public string FirstName { get; private set; }
    public UserRole Role { get; private set; }
    public bool EmailConfirmed { get; private set; }
    public DateTimeOffset? VerificationEmailCooldownEndUtc { get; private set; }
    public int VerificationEmailSendCount { get; private set; }
    public bool VerificationEmailLocked { get; private set; }

    public void RecordVerificationEmailSent(DateTimeOffset nextAllowedAt, int maxAllowedSends)
    {
        VerificationEmailCooldownEndUtc = nextAllowedAt;
        VerificationEmailSendCount++;
        if (VerificationEmailSendCount >= maxAllowedSends)
        {
            VerificationEmailLocked = true;
        }
    }

    public void ResetVerificationEmailState()
    {
        VerificationEmailCooldownEndUtc = null;
        VerificationEmailSendCount = 0;
        VerificationEmailLocked = false;
    }
}
