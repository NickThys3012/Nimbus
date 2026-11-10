using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Nimbus.Infrastructure.Weather;
namespace Nimbus.Infrastructure.Tests;

/// <summary>Issue #46: Open-Meteo through a stubbed handler. Provider problems are failed results; missing variables stay null.</summary>
public class OpenMeteoWindProfileClientTests
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

    private static (OpenMeteoWindProfileClient, Stub) Create(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var stub = new Stub(respond);
        var http = new HttpClient(stub) { BaseAddress = new Uri("https://api.open-meteo.com/v1/") };
        return (new OpenMeteoWindProfileClient(http, NullLogger<OpenMeteoWindProfileClient>.Instance), stub);
    }

    private static HttpResponseMessage Json(string body, HttpStatusCode s = HttpStatusCode.OK) => new(s) { Content = new StringContent(body) };

    [Test]
    public async Task AsksForOneHour_InKnots_WithTheModel_AndReadsValues_LeavingMissingOnesNull()
    {
        var (client, stub) = Create(_ => Json("""{"hourly":{"time":["2026-11-10T09:00"],"wind_speed_10m":[7.5],"wind_speed_950hPa":[null]}}"""));

        var result = await client.GetAsync(51.1568, 4.0439, new DateTime(2026, 11, 10, 9, 0, 0, DateTimeKind.Utc), "icon_d2");

        var q = stub.Last!.Query;
        Assert.That(q, Does.Contain("latitude=51.1568").And.Contain("longitude=4.0439").And.Contain("models=icon_d2")
            .And.Contain("wind_speed_unit=kn").And.Contain("start_hour=2026-11-10T09:00").And.Contain("end_hour=2026-11-10T09:00"));
        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value!.Values["wind_speed_10m"], Is.EqualTo(7.5));
        Assert.That(result.Value.Values["wind_speed_950hPa"], Is.Null);
        Assert.That(result.Value.Values["wind_direction_200m"], Is.Null);
    }

    [TestCase(HttpStatusCode.BadRequest)]
    [TestCase(HttpStatusCode.TooManyRequests)]
    [TestCase(HttpStatusCode.InternalServerError)]
    public async Task AnErrorStatus_IsAFailedResult(HttpStatusCode status)
    {
        var (client, _) = Create(_ => Json("{}", status));

        var result = await client.GetAsync(51, 4, DateTime.UtcNow, "icon_seamless");

        Assert.That(result.IsSuccess, Is.False);
    }

    [Test]
    public async Task GarbageAndMissingHourly_AreFailedResults()
    {
        var (c1, _) = Create(_ => Json("not json"));
        var (c2, _) = Create(_ => Json("{}"));

        Assert.That((await c1.GetAsync(51, 4, DateTime.UtcNow, "icon_seamless")).IsSuccess, Is.False);
        Assert.That((await c2.GetAsync(51, 4, DateTime.UtcNow, "icon_seamless")).IsSuccess, Is.False);
    }

    [Test]
    public async Task AnUnreachableProvider_IsAFailedResult_NotAnException()
    {
        var (client, _) = Create(_ => throw new HttpRequestException("boom"));

        Assert.That((await client.GetAsync(51, 4, DateTime.UtcNow, "icon_seamless")).IsSuccess, Is.False);
    }

    [Test]
    public async Task Series_AsksForTheWholeWindowInOneRequest_ParsesEachHour_AndKeepsNullsNull()
    {
        var (client, stub) = Create(_ => Json("""
            {"hourly":{"time":["2026-11-10T09:00","2026-11-10T10:00","bad"],
              "wind_speed_10m":[7.5,null,3],"wind_speed_950hPa":[8,9,1]}}
            """));

        var result = await client.GetSeriesAsync(51.1568, 4.0439, new DateTime(2026, 11, 10, 9, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 11, 10, 10, 0, 0, DateTimeKind.Utc), "icon_d2");

        Assert.That(stub.Last!.Query, Does.Contain("models=icon_d2").And.Contain("wind_speed_unit=kn").And.Contain("timezone=GMT")
            .And.Contain("start_hour=2026-11-10T09:00").And.Contain("end_hour=2026-11-10T10:00"));
        var hours = result.Value!.Hours;
        Assert.That(hours, Has.Count.EqualTo(2));
        Assert.That(hours[0].HourUtc, Is.EqualTo(new DateTime(2026, 11, 10, 9, 0, 0, DateTimeKind.Utc)));
        Assert.That(hours[0].Values["wind_speed_10m"], Is.EqualTo(7.5));
        Assert.That(hours[1].Values["wind_speed_10m"], Is.Null);
        Assert.That(hours[1].Values["wind_speed_950hPa"], Is.EqualTo(9));
        Assert.That(hours[0].Values["wind_direction_200m"], Is.Null);
    }

    [Test]
    public async Task Series_ProviderProblems_AreFailedResults()
    {
        var from = new DateTime(2026, 11, 10, 9, 0, 0, DateTimeKind.Utc);
        var (status, _) = Create(_ => Json("{}", HttpStatusCode.InternalServerError));
        var (noHourly, _) = Create(_ => Json("{}"));
        var (badJson, _) = Create(_ => Json("nope"));
        var (down, _) = Create(_ => throw new HttpRequestException("no route"));

        foreach (var c in new[] { status, noHourly, badJson, down })
            Assert.That((await c.GetSeriesAsync(51, 4, from, from, "icon_eu")).IsSuccess, Is.False);
    }
}
