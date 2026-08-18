using Amazon.Runtime;
using Amazon.S3;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Nimbus.Application.Common.Interfaces;
using Nimbus.Domain.Interfaces;
using Nimbus.Infrastructure.Identity;
using Nimbus.Infrastructure.Persistence;
using Nimbus.Infrastructure.Persistence.Repositories;
using Nimbus.Infrastructure.Services;
using Nimbus.Infrastructure.Storage;
namespace Nimbus.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    ///     Add dbContext, register repositories and services and add health checks
    /// </summary>
    /// <param name="services"></param>
    /// <param name="config"></param>
    /// <exception cref="ArgumentNullException"></exception>
public static void AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var connectionString = config.GetConnectionString("Database") ?? throw new InvalidOperationException("Connection string 'Database' is not configured.");

        services.AddDbContext<AppDbContext>(opts =>
        {
            opts.UseSqlServer(connectionString, sql =>
            {
                // The database container restarting mid-deploy is the realistic transient
                // fault here, not exotic network partitions — retry a handful of times
                // with EF Core's built-in exponential backoff before giving up.
                sql.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(10),
                    errorNumbersToAdd: null);
            });
        });

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IEmailAuditLogger, EmailAuditLogger>();

        AddObjectStorage(services, config);

        // Tagged "ready" (issue #97): /health/ready runs only checks carrying this tag, so
        // SQL Server and MinIO gate readiness while /health/live — which runs no checks at
        // all — stays a pure "is the process up" probe.
        services.AddHealthChecks()
            .AddSqlServer(connectionString, tags: ["ready"])
            .AddDbContextCheck<AppDbContext>(tags: ["ready"])
            .AddCheck<MinioHealthCheck>("minio", tags: ["ready"]);
    }

    private static void AddObjectStorage(IServiceCollection services, IConfiguration config)
    {
        services
            .AddOptions<StorageOptions>()
            .Bind(config.GetSection(StorageOptions.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.Endpoint), "Storage:Endpoint must be configured.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.AccessKey), "Storage:AccessKey must be configured.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.SecretKey), "Storage:SecretKey must be configured.");

        services.AddSingleton<IAmazonS3>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<StorageOptions>>().Value;
            var s3Config = new AmazonS3Config
            {
                ServiceURL = options.Endpoint, ForcePathStyle = options.ForcePathStyle, UseHttp = !options.UseHttps, AuthenticationRegion = options.Region
            };

            return new AmazonS3Client(new BasicAWSCredentials(options.AccessKey, options.SecretKey), s3Config);
        });

        services.AddScoped<IObjectStorageService, S3ObjectStorageService>();
    }

    /// <summary>
    ///     Add identity services
    /// </summary>
    public static void AddIdentityServices(this IServiceCollection services)
    {
        services
            .AddIdentity<ApplicationUser, IdentityRole>(opts =>
            {
                opts.Password.RequireDigit = true;
                opts.Password.RequiredLength = 8;
                opts.Password.RequireNonAlphanumeric = false;
            })
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();
    }
}
