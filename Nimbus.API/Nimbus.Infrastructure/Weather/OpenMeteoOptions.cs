namespace Nimbus.Infrastructure.Weather;

/// <summary>Configuration for Open-Meteo (bound from the <c>OpenMeteo</c> section): address, timeout and user agent are configuration, not literals.</summary>
public sealed class OpenMeteoOptions
{
    public const string SectionName = "OpenMeteo";

    public string BaseUrl { get; init; } = "https://api.open-meteo.com/v1/";

    public int TimeoutSeconds { get; init; } = 10;

    public string UserAgent { get; init; } = "Nimbus/1.0 (flight preparation)";
}
