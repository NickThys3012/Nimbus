using Nimbus.Application.Common.Interfaces;
using Prometheus;
namespace Nimbus.Observability.Services;

public class PrometheusBusinessMetrics : IBusinessMetrics
{
    private readonly Counter _userFetchedByEmailCounter;
    public PrometheusBusinessMetrics(IMetricFactory? metricsFactory = null)
    {
        metricsFactory ??= Metrics.DefaultFactory;

        _userFetchedByEmailCounter = metricsFactory.CreateCounter("user_fetched_by_email_counter", "Number of users fetched by email");

        // Touch each metric so it is published at zero before the first observation.
        _userFetchedByEmailCounter.IncTo(0);
    }

    public void UserFetchedByEmail()
    {
        _userFetchedByEmailCounter.Inc();
    }
}
