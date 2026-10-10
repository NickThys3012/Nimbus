using Nimbus.Domain.Entities;
namespace Nimbus.Domain.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id);
    Task<User?> GetByEmailAsync(string email);
    /// <summary>Admin use only. Loads every user with roles in a fixed number of queries.</summary>
    Task<IReadOnlyList<User>> ListAllAsync(CancellationToken ct = default);
    Task UpdateAsync(User user);
}
