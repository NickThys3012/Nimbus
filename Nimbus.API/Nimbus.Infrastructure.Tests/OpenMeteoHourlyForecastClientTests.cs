using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Nimbus.Infrastructure.Weather;
namespace Nimbus.Infrastructure.Tests;

/// <summary>Issue #45: Open-Meteo through a stubbed handler. Several models come back suffixed, one model plain; provider problems are failed results.</summary>
public class OpenMeteoHourlyForecastClientTests
{
    private sealed class Stub(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public Uri? Last;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Last = request.RequestUri;
            return Task.FromResult(respond(request));
        }
    }

    private static (OpenMeteoHourlyForecastClient, Stub) Create(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var stub = new Stub(respond);
        var http = new HttpClient(stub) { BaseAddress = new Uri("https://api.open-meteo.com/v1/") };
        return (new OpenMeteoHourlyForecastClient(http, NullLogger<OpenMeteoHourlyForecastClient>.Instance), stub);
    }

    private static HttpResponseMessage Json(string body, HttpStatusCode s = HttpStatusCode.OK) => new(s) { Content = new StringContent(body) };

    [Test]
    public async Task AsksForTheSiteTimezone_InKnots_ForTheDays_AndReadsEachModelSuffixed()
    {
        var (client, stub) = Create(_ => Json("""
            {"timezone":"Europe/Brussels","utc_offset_seconds":3600,
             "hourly":{"time":["2026-11-10T09:00","2026-11-10T10:00"],
               "wind_speed_10m_icon_d2":[8.5,null],"wind_speed_10m_icon_eu":[9,7.5],"temperature_2m_icon_d2":[11.2,null]},
             "daily":{"time":["2026-11-10"],"sunrise":["2026-11-10T07:32"],"sunset":["2026-11-10T16:51"]}}
            """));

        var result = await client.GetAsync(51.1568, 4.0439, ["icon_d2", "icon_eu"], 3);

        var q = stub.Last!.Query;
        Assert.That(q, Does.Contain("latitude=51.1568").And.Contain("longitude=4.0439").And.Contain("models=icon_d2%2Cicon_eu")
            .And.Contain("wind_speed_unit=kn").And.Contain("timezone=auto").And.Contain("forecast_days=3").And.Contain("daily=sunrise,sunset")
            .And.Contain("wind_gusts_10m").And.Contain("cape").And.Contain("precipitation_probability"));
        Assert.That(result.IsSuccess, Is.True);
        var raw = result.Value!;
        Assert.That(raw.TimeZone, Is.EqualTo("Europe/Brussels"));
        Assert.That(raw.UtcOffsetSeconds, Is.EqualTo(3600));
        Assert.That(raw.Hours[0].ByModel["icon_d2"].WindSpeedKt, Is.EqualTo(8.5));
        Assert.That(raw.Hours[1].ByModel["icon_d2"].WindSpeedKt, Is.Null);
        Assert.That(raw.Hours[1].ByModel["icon_eu"].WindSpeedKt, Is.EqualTo(7.5));
        Assert.That(raw.Hours[0].ByModel["icon_eu"].CapeJPerKg, Is.Null);
        Assert.That(raw.Days.Single().Sunrise, Is.EqualTo("2026-11-10T07:32"));
    }

    [Test]
    public async Task ASingleModel_ComesBackWithPlainNames()
    {
        var (client, _) = Create(_ => Json("""{"hourly":{"time":["2026-11-10T09:00"],"wind_speed_10m":[6]}}"""));

        var result = await client.GetAsync(51, 4, ["gfs_global"], 3);

        Assert.That(result.Value!.Hours.Single().ByModel["gfs_global"].WindSpeedKt, Is.EqualTo(6));
    }

    [TestCase(HttpStatusCode.BadRequest)]
    [TestCase(HttpStatusCode.TooManyRequests)]
    [TestCase(HttpStatusCode.InternalServerError)]
    public async Task AnErrorStatus_IsAFailedResult(HttpStatusCode status)
    {
        var (client, _) = Create(_ => Json("{}", status));

        var result = await client.GetAsync(51, 4, ["icon_eu"], 3);

        Assert.That(result.IsSuccess, Is.False);
    }

    [Test]
    public async Task NoHourlyData_AndInvalidJson_AreFailedResults()
    {
        var (noHourly, _) = Create(_ => Json("{}"));
        var (badJson, _) = Create(_ => Json("not json"));

        Assert.That((await noHourly.GetAsync(51, 4, ["icon_eu"], 3)).IsSuccess, Is.False);
        Assert.That((await badJson.GetAsync(51, 4, ["icon_eu"], 3)).IsSuccess, Is.False);
    }

    [Test]
    public async Task AnUnreachableProvider_IsAFailedResult_NotAnException()
    {
        var (client, _) = Create(_ => throw new HttpRequestException("no route"));

        var result = await client.GetAsync(51, 4, ["icon_eu"], 3);

        Assert.That(result.IsSuccess, Is.False);
    }
}
