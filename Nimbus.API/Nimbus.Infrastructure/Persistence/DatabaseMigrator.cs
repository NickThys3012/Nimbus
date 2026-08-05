using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace Nimbus.Infrastructure.Persistence;

public static class DatabaseMigrator
{
    /// <summary>
    ///     Applies any pending EF Core migrations to the database. Run on startup so
    ///     a freshly provisioned database (e.g. the Azure SQL instance) gets its
    ///     schema created before the app reads or seeds data. The App Service runs
    ///     inside Azure, so it reaches the firewalled SQL server that external CI
    ///     runners cannot.
    /// </summary>
    public static async Task MigrateDatabaseAsync(this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
    }
}
