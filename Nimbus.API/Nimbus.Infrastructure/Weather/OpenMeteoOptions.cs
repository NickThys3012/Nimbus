using Nimbus.Application.Common.Interfaces;
using Nimbus.Infrastructure.Http;
namespace Nimbus.Infrastructure.Weather;

/// <summary>Configuration for Open-Meteo (bound from the <c>OpenMeteo</c> section): address, timeout and user agent are configuration, not literals.</summary>
public sealed class OpenMeteoOptions : IForecastSettings
{
    public const string SectionName = "OpenMeteo";

    public string BaseUrl { get; init; } = "https://api.open-meteo.com/v1/";

    /// <summary>How long one attempt may take before it counts as failed.</summary>
    public int TimeoutSeconds { get; init; } = 10;

    /// <summary>
    ///     Optional commercial API key. Without one the free, non-commercial endpoint is used (<see cref="BaseUrl" /> as is). With one
    ///     requests go to the <c>customer-</c> host (<c>customer-api.open-meteo.com</c>) and carry it as the <c>apikey</c> parameter.
    ///     Set it as an environment variable (<c>OpenMeteo__ApiKey</c>), never in a committed file.
    /// </summary>
    public string? ApiKey { get; init; }

    public bool HasApiKey => !string.IsNullOrWhiteSpace(ApiKey);

    /// <summary>Retries and circuit breaker, shared by every Open-Meteo client.</summary>
    public ResilienceOptions Resilience { get; init; } = new();

    /// <summary>The address requests go to: <see cref="BaseUrl" /> with a trailing slash, on the <c>customer-</c> host when there is an API key.</summary>
    public Uri ResolveBaseAddress()
    {
        var uri = new UriBuilder(BaseUrl.EndsWith('/') ? BaseUrl : BaseUrl + "/");
        if (HasApiKey && !uri.Host.StartsWith("customer-", StringComparison.OrdinalIgnoreCase))
            uri.Host = "customer-" + uri.Host;
        return uri.Uri;
    }

    public string UserAgent { get; init; } = "Nimbus/1.0 (flight preparation)";

    /// <summary>Model of the hourly forecast when a request names none (issue #45). icon_seamless: ICON-D2 2 km, then ICON-EU, then ICON global.</summary>
    public string ForecastModel { get; init; } = "icon_seamless";

    /// <summary>Days the hourly forecast covers.</summary>
    public int ForecastDays { get; init; } = 3;

    /// <summary>Minutes a fetched wind series is cached for (issue #47), so re-running a simulation does not refetch.</summary>
    public int SeriesCacheMinutes { get; init; } = 60;

    string IForecastSettings.DefaultModel => ForecastModel;

    int IForecastSettings.Days => ForecastDays;
}
