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
    extension(IServiceCollection services)
    {
        /// <summary>
        ///     Add dbContext, register repositories and services and add health checks
        /// </summary>
        /// <param name="config"></param>
        /// <exception cref="ArgumentNullException"></exception>
        public void AddInfrastructure(IConfiguration config)
        {
            var connectionString = config.GetConnectionString("Database") ?? throw new ArgumentNullException(nameof(config));

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

            services.AddObjectStorage(config);

            services.AddHealthChecks()
                .AddSqlServer(connectionString)
                .AddDbContextCheck<AppDbContext>();
        }

        /// <summary>
        ///     Bind <see cref="StorageOptions" /> and register the S3-compatible object storage client
        ///     (issue #11). Endpoint, credentials, region and path-style addressing all come from the
        ///     <c>Storage</c> config section, so the identical registration serves development
        ///     (self-hosted MinIO) and production alike.
        /// </summary>
        private void AddObjectStorage(IConfiguration config)
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
        public void AddIdentityServices()
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
}
