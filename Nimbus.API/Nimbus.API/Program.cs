using System.Security.Claims;
using System.Text;
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
using Nimbus.Mailing;
using Nimbus.Observability;
using Prometheus;
using Scalar.AspNetCore;
using Serilog;
using SkiaSharp;

// Docker build-time smoke test (issue #96): the trajectory/PDF map rendering features
// (#57, #62) depend on SkiaSharp's native library, which is the single most common way
// a working local build silently breaks in a slim Linux runtime image (missing
// fontconfig/freetype). This renders a tiny bitmap with text and exits immediately —
// no web host, no database — so the Dockerfile can fail the build instead of failing
// the first real map render in production.
if (args.Contains("--render-smoke-test"))
{
    return RunRenderSmokeTest();
}

Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();
try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.AddLogging();

    // Upload-size ceiling (issue #103): must match Caddy's `request_body { max_size 100MB }`
    // in infra/caddy/Caddyfile so one large file cannot exhaust disk. Kestrel is the
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

    builder.Services.AddNimbusEmail(builder.Configuration);
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
    // migration bundle) before this container is ever started — see infra/compose/docker-compose.prod.yml
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

    // Both endpoints are anonymous (no [Authorize], nothing upstream requires a token for
    // /health/*) and both use HealthEndpointResponseWriter (issue #97), which writes only the
    // literal string "Healthy"/"Unhealthy" — no check names, exception messages, version string
    // or anything else that could hint at infrastructure topology to an anonymous caller.

    // No dependency checks run here (Predicate matches nothing) — this answers as soon as the
    // process can serve a request, which is exactly what the docker-compose `healthcheck:` for
    // the `api` container polls every few seconds to decide whether to keep routing traffic to
    // this instance, without ever touching SQL Server or MinIO.
    app.MapHealthChecks("/health/live", new HealthCheckOptions
    {
        ResultStatusCodes =
        {
            [HealthStatus.Healthy] = StatusCodes.Status200OK, [HealthStatus.Degraded] = StatusCodes.Status200OK, [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
        },
        ResponseWriter = HealthEndpointResponseWriter.WriteAsync,
        Predicate = _ => false
    });

    // Runs every check tagged "ready" (SQL Server + MinIO — see AddInfrastructure). This is
    // what the CD workflow (issue #6) polls after swapping in the new `api` container, and what
    // the external uptime monitor and Caddy alike should treat as "can this instance actually
    // serve" — a deploy or an alert fires on the same truth, not a looser/tighter proxy for it.
    app.MapHealthChecks("/health/ready", new HealthCheckOptions
    {
        ResultStatusCodes =
        {
            [HealthStatus.Healthy] = StatusCodes.Status200OK, [HealthStatus.Degraded] = StatusCodes.Status200OK, [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
        },
        ResponseWriter = HealthEndpointResponseWriter.WriteAsync,
        Predicate = check => check.Tags.Contains("ready")
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
    return 1;
}
finally { Log.CloseAndFlush(); }

return 0;

// Renders a small bitmap with a text label — text rendering is what actually
// exercises fontconfig, which is the dependency a slim runtime image is missing
// when everything else "looks" fine. Returns non-zero so a Dockerfile RUN step
// fails the build rather than shipping a broken image.
int RunRenderSmokeTest()
{
    try
    {
        using var bitmap = new SKBitmap(64, 64);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.CornflowerBlue);
        using var font = new SKFont();
        font.Size = 12;
        using var paint = new SKPaint();
        paint.Color = SKColors.White;
        canvas.DrawText("OK", 8, 32, SKTextAlign.Left, font, paint);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        if (data is null || data.Size == 0)
        {
            Console.Error.WriteLine("Skia render smoke test produced no image data.");
            return 1;
        }

        Console.WriteLine($"Skia render smoke test OK ({data.Size} bytes).");
        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Skia render smoke test failed: {ex}");
        return 1;
    }
}
