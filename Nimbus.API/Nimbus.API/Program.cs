using System.Security.Claims;
using System.Text;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using Nimbus.API.Middleware;
using Nimbus.Application;
using Nimbus.Infrastructure;
using Nimbus.Infrastructure.Identity;
using Nimbus.Infrastructure.Persistence;
using Nimbus.Logging;
using Nimbus.Observability;
using Prometheus;
using Scalar.AspNetCore;
using Serilog;
Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();
try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.AddLogging();

    // Upload-size ceiling (issue #103): must match Caddy's `request_body { max_size 100MB }`
    // in infra/Caddyfile so one large file cannot exhaust disk. Kestrel is the
    // defense-in-depth layer behind Caddy — individual endpoints accepting uploads
    // should additionally use [RequestSizeLimit] if they need a tighter cap.
    builder.WebHost.ConfigureKestrel(opts =>
    {
        opts.Limits.MaxRequestBodySize = 100 * 1024 * 1024; // 100 MB
    });

    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);

    builder.Services.AddObservabilityMetrics();

    builder.Services.AddIdentityServices();

    // ── JWT ───────────────────────────────────────────────────────────
    builder.Services
        .AddAuthentication(opts =>
        {
            opts.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            opts.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(opts =>
        {
            // Ensure JWT "role"/"nameid" claims are mapped to ClaimTypes so
            // [Authorize(Roles=...)] and CurrentUserService continue to work.
            opts.MapInboundClaims = true;
            opts.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                NameClaimType = ClaimTypes.Name,
                RoleClaimType = ClaimTypes.Role,
                ValidIssuer = builder.Configuration["Jwt:Issuer"],
                ValidAudience = builder.Configuration["Jwt:Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Secret"]!)),
                ClockSkew = TimeSpan.Zero
            };
        });

    builder.Services.AddHsts(options =>
    {
        options.MaxAge = TimeSpan.FromDays(365); // 31536000 seconds — matches your AC
        options.IncludeSubDomains = true;
        options.Preload = false; // Don't set true unless you're submitting to the HSTS preload list
    });


    builder.Services.AddAuthorization();
    builder.Services.AddScoped<TokenService>();
    // Add services to the container.
    builder.Services.AddControllers();
    // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
    builder.Services.AddOpenApi();

    if (builder.Environment.IsDevelopment())
    {
        var dist = Path.GetFullPath(Path.Combine(
            builder.Environment.ContentRootPath, "../../Nimbus.Web/dist/Nimbus.Web/browser"));
        if (Directory.Exists(dist))
        {
            builder.Environment.WebRootPath = dist;
            // WebRootFileProvider is initialized before this point (based on the
            // default "wwwroot" folder), so it must be rebuilt explicitly — just
            // updating WebRootPath does not refresh the file provider used by
            // UseStaticFiles()/MapFallbackToFile().
            builder.Environment.WebRootFileProvider = new PhysicalFileProvider(dist);
        }
    }
    
    var app = builder.Build();

    // Schema migrations are applied by the dedicated `migrator` container (an EF Core
    // migration bundle) before this container is ever started — see infra/docker-compose.prod.yml
    // (`api` depends_on `migrator: condition: service_completed_successfully`). The API itself
    // carries no migration responsibility and makes no single-instance assumption.
    await app.Services.SeedUsers();

    // ── Middleware pipeline ───────────────────────────────────────────
    app.UseMiddleware<ExceptionHandlingMiddleware>(); // ← must be first
    app.UseSerilogRequestLogging();                   // HTTP request logging (#48)
    app.UseHttpsRedirection();
    app.UseHsts(); // Only sends the header over HTTPS — correct behaviour
    app.UseStaticFiles();

    app.MapStaticAssets();


    // Auto-track HTTP metrics: http_requests_received_total,
    // http_request_duration_seconds, http_requests_in_progress (#47).
    app.UseHttpMetrics();

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapHealthChecks("/health", new HealthCheckOptions
    {
        ResultStatusCodes =
        {
            [HealthStatus.Healthy] = StatusCodes.Status200OK, [HealthStatus.Degraded] = StatusCodes.Status200OK, [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
        },
        ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse,
        Predicate = _ => true
    });

    app.MapOpenApi();
    app.MapScalarApiReference();

    app.MapControllers();
    app.MapMetrics();                    // Prometheus scrape endpoint at /metrics (#47)
    app.MapFallbackToFile("index.html"); // Blazor client-side routing

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Nimbus.Api terminated unexpectedly");
}
finally { Log.CloseAndFlush(); }
