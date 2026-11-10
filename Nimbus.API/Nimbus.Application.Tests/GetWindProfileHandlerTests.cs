using Nimbus.Application.Common;
using Nimbus.Application.Common.Exceptions;
using Nimbus.Application.Common.Interfaces;
using Nimbus.Application.Features.Weather;
using Nimbus.Application.Features.Weather.Queries.GetWindProfile;
namespace Nimbus.Application.Tests;

/// <summary>Issue #46: reads only. A down provider is a 502, a model without all levels is a 422, a bad model never reaches the provider.</summary>
public class GetWindProfileHandlerTests
{
    private sealed class FakeClient(ProviderResult<RawWindProfile> result) : IWindProfileClient
    {
        public string? Model; public DateTime? Hour; public int Calls;
        public Task<ProviderResult<RawWindProfile>> GetAsync(double latitude, double longitude, DateTime hourUtc, string model, CancellationToken ct = default)
        {
            Calls++; Model = model; Hour = hourUtc;
            return Task.FromResult(result);
        }
    }

    private sealed class Settings : IForecastSettings
    {
        public string DefaultModel => "icon_seamless";
        public int Days => 3;
        public int SeriesCacheMinutes => 60;
    }

    private sealed class Clock(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }

    private static RawWindProfile Raw(params string[] without)
    {
        var v = new Dictionary<string, double?>();
        foreach (var name in WindProfileMapper.RequestedVariables) v[name] = without.Contains(name) ? null : 8;
        return new RawWindProfile(v);
    }

    private static GetWindProfileQueryHandler Handler(FakeClient c) => new(c, new Settings(), new Clock(new DateTimeOffset(2026, 11, 10, 9, 41, 0, TimeSpan.Zero)));

    [Test]
    public async Task Defaults_ToIconSeamless_AndTheCurrentHour()
    {
        var client = new FakeClient(ProviderResult.Ok(Raw()));

        var dto = await Handler(client).Handle(new GetWindProfileQuery(51.1568, 4.0439), default);

        Assert.That(client.Model, Is.EqualTo("icon_seamless"));
        Assert.That(dto.ForecastHourUtc, Is.EqualTo(new DateTime(2026, 11, 10, 9, 0, 0, DateTimeKind.Utc)));
        Assert.That(dto.Levels, Has.Count.EqualTo(10));
    }

    [Test]
    public async Task AProviderOutage_Is502()
    {
        var client = new FakeClient(ProviderResult.Fail<RawWindProfile>("down"));

        Assert.ThrowsAsync<ProviderUnavailableException>(() => Handler(client).Handle(new GetWindProfileQuery(51, 4), default));
    }

    [Test]
    public void AModelMissingALevel_IsRejectedWithAValidationError_NamingTheLevel()
    {
        var client = new FakeClient(ProviderResult.Ok(Raw("wind_speed_950hPa")));

        var ex = Assert.ThrowsAsync<Nimbus.Application.Common.Exceptions.ValidationException>(() => Handler(client).Handle(new GetWindProfileQuery(51, 4), default));

        Assert.That(ex!.Errors["Model"][0], Does.Contain("950 hPa"));
    }

    [TestCase("ecmwf_ifs", false)]
    [TestCase("jma_gsm", false)]
    [TestCase("icon_d2", true)]
    [TestCase(null, true)]
    public void OnlyModelsWithAllLevelsAreAccepted(string? model, bool valid)
    {
        var result = new GetWindProfileQueryValidator().Validate(new GetWindProfileQuery(51, 4, null, model));

        Assert.That(result.IsValid, Is.EqualTo(valid));
    }

    [TestCase(91, 4)]
    [TestCase(51, 181)]
    public void CoordinatesOutOfRange_AreRejected(double lat, double lon)
    {
        Assert.That(new GetWindProfileQueryValidator().Validate(new GetWindProfileQuery(lat, lon)).IsValid, Is.False);
    }
}
