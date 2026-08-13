using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
namespace Nimbus.Infrastructure.Persistence;

/// <summary>
///     Design-time factory for <see cref="AppDbContext" /> (issue #2). Without this, EF Core
///     tooling — `dotnet ef migrations add/bundle` and the resulting migration bundle at
///     runtime — falls back to invoking the full application host (<c>Program.cs</c>) to
///     resolve a <see cref="AppDbContext" />, which drags in unrelated startup dependencies
///     (JWT secrets, object storage, Serilog sinks) the migrator container has no business
///     needing. This factory builds just enough — a connection string — to construct the
///     context directly, keeping the migrator's footprint limited to the database connection.
/// </summary>
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        // The bundle passes the connection string as `--connection "<value>"` (see the
        // repo-root Dockerfile's `migrator` entrypoint); `dotnet ef` at design time falls
        // back to the environment variable ASP.NET Core's own config binding would use.
        var connectionString = GetConnectionStringArg(args)
            ?? Environment.GetEnvironmentVariable("ConnectionStrings__Database")
            ?? throw new InvalidOperationException(
                "Database connection string not provided. Pass --connection <value> or set ConnectionStrings__Database.");

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseSqlServer(connectionString, sql =>
            sql.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(10),
                errorNumbersToAdd: null));

        return new AppDbContext(optionsBuilder.Options);
    }

    private static string? GetConnectionStringArg(string[] args)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i] is "--connection" or "-c")
            {
                return args[i + 1];
            }
        }

        return null;
    }
}
