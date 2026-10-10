using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
namespace Nimbus.Infrastructure.Persistence;

public class LoginEventRetentionOptions
{
    public const string SectionName = "LoginEvents";

    /// <summary>Login audit rows older than this are deleted.</summary>
    public int RetentionDays { get; set; } = 90;

    /// <summary>How often the purge runs.</summary>
    public int PurgeIntervalHours { get; set; } = 6;
}

/// <summary>Periodically deletes login audit events older than the retention window.</summary>
public sealed class LoginEventRetentionService : BackgroundService
{
    private readonly ILogger<LoginEventRetentionService> _logger;
    private readonly IOptions<LoginEventRetentionOptions> _options;
    private readonly IServiceScopeFactory _scopes;
    private readonly TimeProvider _time;

    public LoginEventRetentionService(
        IServiceScopeFactory scopes,
        IOptions<LoginEventRetentionOptions> options,
        TimeProvider time,
        ILogger<LoginEventRetentionService> logger)
    {
        _scopes = scopes;
        _options = options;
        _time = time;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromHours(Math.Max(1, _options.Value.PurgeIntervalHours));
        using var timer = new PeriodicTimer(interval);
        do
        {
            try
            {
                var deleted = await PurgeAsync(stoppingToken);
                if (deleted > 0)
                {
                    _logger.LogInformation("Purged {Count} login events older than {Days} days", deleted, _options.Value.RetentionDays);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Login event purge failed; will retry at the next interval.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    /// <summary>Deletes events older than the retention window and returns how many were removed.</summary>
    public async Task<int> PurgeAsync(CancellationToken ct)
    {
        var cutoff = _time.GetUtcNow().UtcDateTime.AddDays(-Math.Max(1, _options.Value.RetentionDays));
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.LoginEvents.Where(e => e.OccurredAt < cutoff).ExecuteDeleteAsync(ct);
    }
}
