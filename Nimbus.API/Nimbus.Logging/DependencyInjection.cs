using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Sinks.Grafana.Loki;
namespace Nimbus.Logging;

public static class DependencyInjection
{
    public static void AddLogging(this IHostBuilder builder)
    {
        builder.UseSerilog((context, services, configuration) =>
        {
            configuration
                .ReadFrom.Configuration(context.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                .Enrich.WithMachineName()
                .Enrich.WithThreadId()
                // Must run last so it redacts properties added by every enricher
                // and message-template capture above it (issue #12).
                .Enrich.With<SensitiveDataRedactionEnricher>();

            var lokiUrl = context.Configuration["Loki:Url"];
            if (string.IsNullOrWhiteSpace(lokiUrl))
            {
                return;
            }
            var environment = context.Configuration["Loki:Environment"]
                ?? context.HostingEnvironment.EnvironmentName.ToLowerInvariant();

            configuration.WriteTo.GrafanaLoki(
                lokiUrl,
                [
                    new LokiLabel
                    {
                        Key = "app", Value = "nimbus-api"
                    },
                    new LokiLabel
                    {
                        Key = "environment", Value = environment
                    }
                ],
                textFormatter: new LokiJsonTextFormatter());
        });
    }
}
