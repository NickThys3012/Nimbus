using Nimbus.Application.Common;
using Nimbus.Application.Common.Exceptions;
using Nimbus.Application.Common.Interfaces;
using Nimbus.Application.Features.Weather;
using Nimbus.Application.Features.Weather.Queries.GetHourlyForecast;
namespace Nimbus.Application.Tests;

/// <summary>Issue #45: a seamless model is split so each hour records its model; a down provider is a 502; sunrise/sunset come as local HH:mm.</summary>
public class HourlyForecastTests
{
    private sealed class FakeClient(ProviderResult<RawHourlyForecast> result) : IHourlyForecastClient
    {
        public IReadOnlyList<string>? Models; public int Days; public int Calls;
        public Task<ProviderResult<RawHourlyForecast>> GetAsync(double latitude, double longitude, IReadOnlyList<string> models, int days, CancellationToken ct = default)
        {
            Calls++; Models = models; Days = days;
            return Task.FromResult(result);
        }
    }

    private sealed class Settings : IForecastSettings
    {
        public string DefaultModel => "icon_seamless";
        public int Days => 3;
    }

    private static readonly ForecastValues Empty = new(null, null, null, null, null, null, null);
    private static ForecastValues Wind(double kt) => new(12.34, kt, 365, 14.06, 80.4, 0.25, 70.6);

    private static RawHourlyForecast Raw(params RawForecastHour[] hours) =>
        new("Europe/Brussels", 3600, hours, [new RawForecastDay("2026-11-10", "2026-11-10T07:32", "2026-11-10T16:51")]);

    private static GetHourlyForecastQueryHandler Handler(FakeClient c) => new(c, new Settings());

    [Test]
    public async Task Defaults_ToTheConfiguredModel_AskedForAsItsParts_OverTheConfiguredDays()
    {
        var client = new FakeClient(ProviderResult.Ok(Raw()));

        var dto = await Handler(client).Handle(new GetHourlyForecastQuery(51.1568, 4.0439), default);

        Assert.That(client.Models, Is.EqualTo(new[] { "icon_d2", "icon_eu", "icon_global" }));
        Assert.That(client.Days, Is.EqualTo(3));
        Assert.That(dto.Model, Is.EqualTo("icon_seamless"));
        Assert.That(dto.TimeZone, Is.EqualTo("Europe/Brussels"));
    }

    [Test]
    public async Task AChosenModel_NotSplit_IsAskedForAsIs()
    {
        var client = new FakeClient(ProviderResult.Ok(Raw()));

        await Handler(client).Handle(new GetHourlyForecastQuery(51, 4, " gfs_global "), default);

        Assert.That(client.Models, Is.EqualTo(new[] { "gfs_global" }));
    }

    [Test]
    public void EachHour_TakesTheFirstModelWithData_AndRecordsIt_AndDropsHoursNoModelCovers()
    {
        var raw = Raw(
            new RawForecastHour("2026-11-12T10:00", new Dictionary<string, ForecastValues> { ["icon_d2"] = Wind(9), ["icon_eu"] = Wind(8) }),
            new RawForecastHour("2026-11-12T11:00", new Dictionary<string, ForecastValues> { ["icon_d2"] = Empty, ["icon_eu"] = Wind(7) }),
            new RawForecastHour("2026-11-12T12:00", new Dictionary<string, ForecastValues> { ["icon_d2"] = Empty, ["icon_eu"] = Empty }));

        var dto = HourlyForecastMapper.Map("icon_seamless", ["icon_d2", "icon_eu"], raw);

        Assert.That(dto.Hours.Select(h => (h.Time, h.Model)), Is.EqualTo(new[] { ("2026-11-12T10:00", "icon_d2"), ("2026-11-12T11:00", "icon_eu") }));
    }

    [Test]
    public void Values_AreRounded_DirectionIsNormalised_AndAMissingValueStaysNull()
    {
        var raw = Raw(new RawForecastHour("2026-11-10T09:00", new Dictionary<string, ForecastValues>
        {
            ["icon_eu"] = new(12.34, 9.26, 365, null, 80.4, 0.25, 70.6)
        }));

        var h = HourlyForecastMapper.Map("icon_eu", ["icon_eu"], raw).Hours.Single();

        Assert.That(h.TemperatureC, Is.EqualTo(12.3m));
        Assert.That(h.WindSpeedKt, Is.EqualTo(9.3m));
        Assert.That(h.WindDirectionDeg, Is.EqualTo(5));
        Assert.That(h.WindGustKt, Is.Null);
        Assert.That(h.CapeJPerKg, Is.EqualTo(80m));
        Assert.That(h.PrecipitationMm, Is.EqualTo(0.3m));
        Assert.That(h.PrecipitationProbabilityPct, Is.EqualTo(71));
    }

    [Test]
    public void Days_CarryLocalSunriseAndSunset_AsHoursAndMinutes()
    {
        var dto = HourlyForecastMapper.Map("icon_eu", ["icon_eu"], Raw());

        Assert.That(dto.Days.Single(), Is.EqualTo(new Nimbus.Contracts.DTOs.Features.Weather.ForecastDayDto("2026-11-10", "07:32", "16:51")));
    }

    [Test]
    public void ASunThatDoesNotRise_HasNoSunriseTime()
    {
        var raw = new RawHourlyForecast("Arctic/Longyearbyen", 3600, [], [new RawForecastDay("2026-12-20", null, null)]);

        var day = HourlyForecastMapper.Map("icon_eu", ["icon_eu"], raw).Days.Single();

        Assert.That(day.Sunrise, Is.Null);
        Assert.That(day.Sunset, Is.Null);
    }

    [Test]
    public void AProviderOutage_Is502()
    {
        var client = new FakeClient(ProviderResult.Fail<RawHourlyForecast>("down"));

        Assert.ThrowsAsync<ProviderUnavailableException>(() => Handler(client).Handle(new GetHourlyForecastQuery(51, 4), default));
    }

    [Test]
    public void Validator_RejectsAnUnsupportedModel_AndBadCoordinates()
    {
        var v = new GetHourlyForecastQueryValidator();

        Assert.That(v.Validate(new GetHourlyForecastQuery(51, 4, "metno_nordic")).IsValid, Is.False);
        Assert.That(v.Validate(new GetHourlyForecastQuery(91, 4)).IsValid, Is.False);
        Assert.That(v.Validate(new GetHourlyForecastQuery(51, 4, "icon_seamless")).IsValid, Is.True);
        Assert.That(v.Validate(new GetHourlyForecastQuery(51, 4)).IsValid, Is.True);
    }
}
