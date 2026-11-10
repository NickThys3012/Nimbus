using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Nimbus.Application.Common.Interfaces;
using Nimbus.Infrastructure.Http;
using Nimbus.Infrastructure.Weather;
namespace Nimbus.Infrastructure.Tests;

/// <summary>Issue #48: every provider has its own client with its own configured address, timeout, user agent and failure policy.</summary>
public class ExternalProviderRegistrationTests
{
    private static ServiceProvider Build(Dictionary<string, string?>? settings = null, Action<IServiceCollection>? more = null)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(settings ?? []).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddExternalProviders(config);
        more?.Invoke(services);
        return services.BuildServiceProvider();
    }

    [Test]
    public void EveryProvider_HasItsOwnNamedClient_WithItsOwnAddressAndUserAgent()
    {
        using var sp = Build(new()
        {
            ["Overpass:BaseUrl"] = "https://overpass.example/api",
            ["Overpass:UserAgent"] = "Nimbus-overpass",
            ["StaticMapTiles:BaseUrl"] = "https://tiles.example/",
            ["StaticMapTiles:UserAgent"] = "Nimbus-tiles"
        });
        var factory = sp.GetRequiredService<IHttpClientFactory>();

        var overpass = factory.CreateClient(ProviderClients.Overpass);
        var tiles = factory.CreateClient(ProviderClients.StaticMapTiles);

        Assert.That(overpass.BaseAddress, Is.EqualTo(new Uri("https://overpass.example/api/")));
        Assert.That(overpass.DefaultRequestHeaders.UserAgent.ToString(), Is.EqualTo("Nimbus-overpass"));
        Assert.That(tiles.BaseAddress, Is.EqualTo(new Uri("https://tiles.example/")));
        Assert.That(tiles.DefaultRequestHeaders.UserAgent.ToString(), Is.EqualTo("Nimbus-tiles"));
    }

    [Test]
    public void TheTypedClients_ResolveWithTheirOwnConfiguredBaseAddress()
    {
        using var sp = Build(new()
        {
            ["AviationWeather:BaseUrl"] = "https://avwx.example/data/",
            ["OpenMeteo:BaseUrl"] = "https://meteo.example/v1"
        });

        Assert.That(sp.GetRequiredService<IAviationWeatherClient>(), Is.Not.Null);
        Assert.That(sp.GetRequiredService<IWindProfileClient>(), Is.Not.Null);
        Assert.That(sp.GetRequiredService<IWindSeriesClient>(), Is.Not.Null);
        Assert.That(sp.GetRequiredService<IHourlyForecastClient>(), Is.Not.Null);
        Assert.That(sp.GetRequiredService<IForecastSettings>().DefaultModel, Is.EqualTo("icon_seamless"));
    }

    [Test]
    public void Timeouts_AreConfiguration_AndOutOfRangeOnesAreRejected()
    {
        using var sp = Build(new() { ["OpenMeteo:TimeoutSeconds"] = "0", ["Overpass:TimeoutSeconds"] = "61", ["AviationWeather:TimeoutSeconds"] = "9" });

        Assert.That(() => sp.GetRequiredService<IOptions<OpenMeteoOptions>>().Value, Throws.TypeOf<OptionsValidationException>());
        Assert.That(() => sp.GetRequiredService<IOptions<OverpassOptions>>().Value, Throws.TypeOf<OptionsValidationException>());
        Assert.That(sp.GetRequiredService<IOptions<AviationWeatherOptions>>().Value.TimeoutSeconds, Is.EqualTo(9));
    }

    [TestCase("gfs_global")]
    [TestCase("icon_nonsense")]
    [TestCase("ecmwf_aifs025_single")]
    public void TheForecastModel_MustBeOneThatOffersEveryWindLevel(string model)
    {
        using var sp = Build(new() { ["OpenMeteo:ForecastModel"] = model });

        var ex = Assert.Throws<OptionsValidationException>(() => _ = sp.GetRequiredService<IOptions<OpenMeteoOptions>>().Value);
        Assert.That(ex!.Message, Does.Contain("ForecastModel"));
    }

    [Test]
    public void ASupportedForecastModel_IsAccepted_AndBecomesTheDefault()
    {
        using var sp = Build(new() { ["OpenMeteo:ForecastModel"] = "meteofrance_seamless" });

        Assert.That(sp.GetRequiredService<IForecastSettings>().DefaultModel, Is.EqualTo("meteofrance_seamless"));
    }

    [TestCase("MaxRetries", "9")]
    [TestCase("RetryBaseDelayMs", "0")]
    [TestCase("BreakAfterFailures", "1")]
    [TestCase("BreakDurationSeconds", "0")]
    public void AnOutOfRangeFailurePolicy_IsRejected(string key, string value)
    {
        using var sp = Build(new() { [$"OpenMeteo:Resilience:{key}"] = value });

        Assert.That(() => sp.GetRequiredService<IOptions<OpenMeteoOptions>>().Value, Throws.TypeOf<OptionsValidationException>());
    }

    [Test]
    public void WithoutAnApiKey_TheFreeEndpointIsUsed()
    {
        var options = new OpenMeteoOptions();

        Assert.That(options.HasApiKey, Is.False);
        Assert.That(options.ResolveBaseAddress(), Is.EqualTo(new Uri("https://api.open-meteo.com/v1/")));
    }

    [Test]
    public void WithAnApiKey_TheCustomerHostIsUsed_OnceOnly()
    {
        Assert.That(new OpenMeteoOptions { ApiKey = "secret" }.ResolveBaseAddress(), Is.EqualTo(new Uri("https://customer-api.open-meteo.com/v1/")));
        Assert.That(new OpenMeteoOptions { ApiKey = "secret", BaseUrl = "https://customer-api.open-meteo.com/v1" }.ResolveBaseAddress(),
            Is.EqualTo(new Uri("https://customer-api.open-meteo.com/v1/")));
        Assert.That(new OpenMeteoOptions { ApiKey = "  " }.ResolveBaseAddress().Host, Is.EqualTo("api.open-meteo.com"));
    }

    private sealed class Capture : HttpMessageHandler
    {
        public Uri? Uri;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Uri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }

    [Test]
    public async Task WithAnApiKey_EveryRequestCarriesTheApikeyParameter()
    {
        var capture = new Capture();
        var handler = new OpenMeteoApiKeyHandler(Options.Create(new OpenMeteoOptions { ApiKey = "k e/y" })) { InnerHandler = capture };

        await new HttpClient(handler).GetAsync("https://customer-api.open-meteo.com/v1/forecast?latitude=51");

        Assert.That(capture.Uri!.Query, Is.EqualTo("?latitude=51&apikey=k%20e%2Fy"));
    }

    [Test]
    public async Task WithoutAnApiKey_NoApikeyParameterIsAdded()
    {
        var capture = new Capture();
        var handler = new OpenMeteoApiKeyHandler(Options.Create(new OpenMeteoOptions())) { InnerHandler = capture };

        await new HttpClient(handler).GetAsync("https://api.open-meteo.com/v1/forecast?latitude=51");

        Assert.That(capture.Uri!.Query, Is.EqualTo("?latitude=51"));
    }
}
