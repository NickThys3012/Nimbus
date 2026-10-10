using Nimbus.Domain.Entities;
namespace Nimbus.Domain.Interfaces;

public interface ILoginEventRepository
{
    Task AddAsync(LoginEvent loginEvent, CancellationToken ct = default);

    /// <summary>Newest first, bounded by <paramref name="limit" />. Admin use only.</summary>
    Task<IReadOnlyList<LoginEvent>> ListRecentAsync(int limit, CancellationToken ct = default);
}
