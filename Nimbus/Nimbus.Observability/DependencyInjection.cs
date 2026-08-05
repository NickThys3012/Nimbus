using Microsoft.Extensions.DependencyInjection;
using Nimbus.Application.Common.Interfaces;
using Nimbus.Observability.Services;
namespace Nimbus.Observability;

public static class DependencyInjection
{
    public static void AddObservabilityMetrics(this IServiceCollection services)
    {
        services.AddSingleton<IBusinessMetrics, PrometheusBusinessMetrics>();
    }
}
