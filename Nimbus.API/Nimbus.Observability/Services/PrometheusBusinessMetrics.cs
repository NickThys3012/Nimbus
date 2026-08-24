using Nimbus.Application.Common.Interfaces;
using Prometheus;
namespace Nimbus.Observability.Services;

public class PrometheusBusinessMetrics : IBusinessMetrics
{
    private readonly Counter _userFetchedByEmailCounter;
    private readonly Counter _usersRegisteredCounter;
    public PrometheusBusinessMetrics(IMetricFactory? metricsFactory = null)
    {
        metricsFactory ??= Metrics.DefaultFactory;

        _userFetchedByEmailCounter = metricsFactory.CreateCounter("user_fetched_by_email_counter", "Number of users fetched by email");
        _usersRegisteredCounter = metricsFactory.CreateCounter("users_registered_counter", "Number of users registered");

        // Touch each metric so it is published at zero before the first observation.
        _userFetchedByEmailCounter.IncTo(0);
        _usersRegisteredCounter.IncTo(0);
    }

    public void UserFetchedByEmail()
    {
        _userFetchedByEmailCounter.Inc();
    }
    public void UsersRegistered()
    {
        _usersRegisteredCounter.Inc();
    }
}
