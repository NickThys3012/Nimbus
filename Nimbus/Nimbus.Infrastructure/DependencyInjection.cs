using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nimbus.Application.Common.Interfaces;
using Nimbus.Domain.Interfaces;
using Nimbus.Infrastructure.Identity;
using Nimbus.Infrastructure.Persistence;
using Nimbus.Infrastructure.Persistence.Repositories;
using Nimbus.Infrastructure.Services;
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
                opts.UseSqlServer(connectionString);
            });

            services.AddScoped<IUserRepository, UserRepository>();
            services.AddHttpContextAccessor();
            services.AddScoped<ICurrentUserService, CurrentUserService>();

            services.AddHealthChecks()
                .AddSqlServer(connectionString)
                .AddDbContextCheck<AppDbContext>();
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
