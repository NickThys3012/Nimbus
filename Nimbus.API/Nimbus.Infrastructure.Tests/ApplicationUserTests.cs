using Nimbus.Domain.Enums;
using Nimbus.Infrastructure.Identity;
namespace Nimbus.Infrastructure.Tests;

public class ApplicationUserTests
{
    [Test]
    public void NewApplicationUser_DefaultsToPilotRole_NotAdmin()
    {
        Assert.That(new ApplicationUser().Role, Is.EqualTo(UserRole.Pilot));
    }
}
