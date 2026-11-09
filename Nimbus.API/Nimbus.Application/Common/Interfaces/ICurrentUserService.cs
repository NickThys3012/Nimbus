using Nimbus.Domain.Entities;
namespace Nimbus.Application.Common.Interfaces;

public interface ICurrentUserService
{
    Guid UserId { get; }

    /// <summary>True when the authenticated principal has the Admin role (from the token claims, no database call). Fails closed.</summary>
    bool IsAdmin => false;
    Task<User?> GetCurrentUserAsync();
}
