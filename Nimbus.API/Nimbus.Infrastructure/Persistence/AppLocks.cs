using Microsoft.EntityFrameworkCore;
namespace Nimbus.Infrastructure.Persistence;

/// <summary>
///     Transaction-scoped SQL Server application locks. Admin mutations that revoke a user's
///     sessions (block, delete, role change) take the lock Exclusive; refresh-token rotation takes
///     it Shared. Because a rotation either finishes before such a mutation starts (and is then
///     revoked by it) or starts after it committed (and sees the new account state), no refresh
///     token can be minted past the revoke point.
/// </summary>
internal static class AppLocks
{
    public const string AdminSet = "nimbus-admin-set";

    public static async Task AcquireAsync(AppDbContext db, string resource, bool exclusive, CancellationToken ct)
    {
        await db.Database.ExecuteSqlRawAsync(
            "DECLARE @r int; EXEC @r = sp_getapplock @Resource = {0}, @LockMode = {1}, @LockOwner = 'Transaction', @LockTimeout = 10000; IF @r < 0 THROW 50001, 'Could not acquire the application lock.', 1;",
            [resource, exclusive ? "Exclusive" : "Shared"], ct);
    }
}
