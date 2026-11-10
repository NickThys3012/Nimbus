using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Nimbus.Application.Common.Interfaces;
using Nimbus.Application.Features.Weather;
using Nimbus.Infrastructure.Weather;
namespace Nimbus.Infrastructure.Http;

/// <summary>Names of the providers' HTTP clients. A provider has one circuit, shared by all its clients.</summary>
public static class ProviderClients
{
    public const string AviationWeather = "aviation-weather";
    public const string OpenMeteo = "open-meteo";
    public const string Overpass = "overpass";
    public const string StaticMapTiles = "static-map-tiles";
}

/// <summary>
///     Registers every external provider behind its own named HTTP client: its own base address, timeout, user agent and failure
///     policy (retry with backoff and jitter, circuit breaker), all from configuration, so one slow or failing provider cannot hold
///     up another or the form. Provider calls return a <c>ProviderResult</c>; the clients turn a failure into one instead of throwing.
/// </summary>
public static class ExternalProviderRegistration
{
    public static IServiceCollection AddExternalProviders(this IServiceCollection services, IConfiguration config)
    {
        services.AddSingleton<ProviderPipelines>();
        services.AddMemoryCache();

        AddAviationWeather(services, config);
        AddOpenMeteo(services, config);
        AddNamed(services, config, ProviderClients.Overpass, OverpassOptions.SectionName,
            (OverpassOptions o) => new ProviderHttpSettings(o.TimeoutSeconds, o.Resilience), o => o.BaseUrl, o => o.UserAgent);
        AddNamed(services, config, ProviderClients.StaticMapTiles, StaticMapTilesOptions.SectionName,
            (StaticMapTilesOptions o) => new ProviderHttpSettings(o.TimeoutSeconds, o.Resilience), o => o.BaseUrl, o => o.UserAgent);
        return services;
    }

    private static void AddAviationWeather(IServiceCollection services, IConfiguration config)
    {
        AddOptions<AviationWeatherOptions>(services, config, AviationWeatherOptions.SectionName,
            o => o.BaseUrl, o => o.TimeoutSeconds, o => o.UserAgent, o => o.Resilience);

        services
            .AddHttpClient<IAviationWeatherClient, AviationWeatherClient>((sp, http) =>
            {
                var o = sp.GetRequiredService<IOptions<AviationWeatherOptions>>().Value;
                Configure(http, Address(o.BaseUrl), o.UserAgent);
            })
            .AddResilience(ProviderClients.AviationWeather, sp =>
            {
                var o = sp.GetRequiredService<IOptions<AviationWeatherOptions>>().Value;
                return new ProviderHttpSettings(o.TimeoutSeconds, o.Resilience);
            });
    }

    private static void AddOpenMeteo(IServiceCollection services, IConfiguration config)
    {
        AddOptions<OpenMeteoOptions>(services, config, OpenMeteoOptions.SectionName,
            o => o.BaseUrl, o => o.TimeoutSeconds, o => o.UserAgent, o => o.Resilience)
            // The model is shared by the forecast (#45), the wind profile (#46) and the simulation series (#47): it must be one all three accept.
            .Validate(o => WindProfileMapper.SupportedModels.Contains(o.ForecastModel),
                $"OpenMeteo:ForecastModel must be one of: {string.Join(", ", WindProfileMapper.SupportedModels)} (the models that offer every wind level).")
            .Validate(o => o.ForecastDays is >= 1 and <= 7, "OpenMeteo:ForecastDays must be between 1 and 7.")
            .Validate(o => o.SeriesCacheMinutes is >= 1 and <= 180, "OpenMeteo:SeriesCacheMinutes must be between 1 and 180.");

        services.AddSingleton<IForecastSettings>(sp => sp.GetRequiredService<IOptions<OpenMeteoOptions>>().Value);
        services.AddTransient<OpenMeteoApiKeyHandler>();

        // Three typed clients, one provider: they share the Open-Meteo circuit. Logging is off for them because the API key travels
        // in the query string and the default HttpClient logging writes the full address.
        void Add<TClient, TImpl>() where TClient : class where TImpl : class, TClient =>
            services
                .AddHttpClient<TClient, TImpl>((sp, http) =>
                {
                    var o = sp.GetRequiredService<IOptions<OpenMeteoOptions>>().Value;
                    Configure(http, o.ResolveBaseAddress(), o.UserAgent);
                })
                .RemoveAllLoggers()
                .AddHttpMessageHandler<OpenMeteoApiKeyHandler>()
                .AddResilience(ProviderClients.OpenMeteo, sp =>
                {
                    var o = sp.GetRequiredService<IOptions<OpenMeteoOptions>>().Value;
                    return new ProviderHttpSettings(o.TimeoutSeconds, o.Resilience);
                });

        Add<IWindProfileClient, OpenMeteoWindProfileClient>();
        Add<IWindSeriesClient, OpenMeteoWindProfileClient>();
        Add<IHourlyForecastClient, OpenMeteoHourlyForecastClient>();
    }

    /// <summary>A provider that has no client class yet: a named client for <see cref="IHttpClientFactory.CreateClient(string)" />.</summary>
    private static void AddNamed<TOptions>(IServiceCollection services, IConfiguration config, string name, string section,
        Func<TOptions, ProviderHttpSettings> settings, Func<TOptions, string> baseUrl, Func<TOptions, string> userAgent) where TOptions : class
    {
        services
            .AddOptions<TOptions>()
            .Bind(config.GetSection(section))
            .Validate(o => IsHttps(baseUrl(o)), $"{section}:BaseUrl must be an absolute https URL.")
            .Validate(o => settings(o).TimeoutSeconds is >= 1 and <= 60, $"{section}:TimeoutSeconds must be between 1 and 60.")
            .Validate(o => !string.IsNullOrWhiteSpace(userAgent(o)), $"{section}:UserAgent must be configured.")
            .Validate(o => settings(o).Resilience.Validate(section) is null, $"{section}:Resilience is out of range.");

        services
            .AddHttpClient(name, (sp, http) =>
            {
                var o = sp.GetRequiredService<IOptions<TOptions>>().Value;
                Configure(http, Address(baseUrl(o)), userAgent(o));
            })
            .AddResilience(name, sp => settings(sp.GetRequiredService<IOptions<TOptions>>().Value));
    }

    private static OptionsBuilder<TOptions> AddOptions<TOptions>(IServiceCollection services, IConfiguration config, string section,
        Func<TOptions, string> baseUrl, Func<TOptions, int> timeout, Func<TOptions, string> userAgent, Func<TOptions, ResilienceOptions> resilience)
        where TOptions : class =>
        services
            .AddOptions<TOptions>()
            .Bind(config.GetSection(section))
            .Validate(o => IsHttps(baseUrl(o)), $"{section}:BaseUrl must be an absolute https URL.")
            .Validate(o => timeout(o) is >= 1 and <= 60, $"{section}:TimeoutSeconds must be between 1 and 60.")
            .Validate(o => !string.IsNullOrWhiteSpace(userAgent(o)), $"{section}:UserAgent must be configured.")
            .Validate(o => resilience(o).Validate(section) is null, $"{section}:Resilience is out of range.");

    private static IHttpClientBuilder AddResilience(this IHttpClientBuilder builder, string provider, Func<IServiceProvider, ProviderHttpSettings> settings) =>
        builder.AddHttpMessageHandler(sp => new ResilienceHandler(sp.GetRequiredService<ProviderPipelines>().For(provider, settings(sp))));

    private static bool IsHttps(string url) => Uri.TryCreate(url, UriKind.Absolute, out var u) && u.Scheme == Uri.UriSchemeHttps;

    private static Uri Address(string url) => new(url.EndsWith('/') ? url : url + "/");

    private static void Configure(HttpClient http, Uri address, string userAgent)
    {
        http.BaseAddress = address;
        // The per-attempt timeout is the pipeline's, so retries each get the full time; the client's own, whole-call timeout is off.
        http.Timeout = Timeout.InfiniteTimeSpan;
        http.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
    }
}
