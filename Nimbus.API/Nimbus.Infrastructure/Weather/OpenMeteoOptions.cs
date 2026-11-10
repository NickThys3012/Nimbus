using Nimbus.Application.Common.Interfaces;
namespace Nimbus.Infrastructure.Weather;

/// <summary>Configuration for Open-Meteo (bound from the <c>OpenMeteo</c> section): address, timeout and user agent are configuration, not literals.</summary>
public sealed class OpenMeteoOptions : IForecastSettings
{
    public const string SectionName = "OpenMeteo";

    public string BaseUrl { get; init; } = "https://api.open-meteo.com/v1/";

    public int TimeoutSeconds { get; init; } = 10;

    public string UserAgent { get; init; } = "Nimbus/1.0 (flight preparation)";

    /// <summary>Model of the hourly forecast when a request names none (issue #45). icon_seamless: ICON-D2 2 km, then ICON-EU, then ICON global.</summary>
    public string ForecastModel { get; init; } = "icon_seamless";

    /// <summary>Days the hourly forecast covers.</summary>
    public int ForecastDays { get; init; } = 3;

    string IForecastSettings.DefaultModel => ForecastModel;

    int IForecastSettings.Days => ForecastDays;
}
