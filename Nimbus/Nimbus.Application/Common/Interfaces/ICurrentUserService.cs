using Nimbus.Domain.Entities;
namespace Nimbus.Application.Common.Interfaces;

public interface ICurrentUserService
{
    Guid UserId { get; }
    Task<User?> GetCurrentUserAsync();
}
