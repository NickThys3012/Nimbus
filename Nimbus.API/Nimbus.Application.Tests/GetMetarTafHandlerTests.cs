using Nimbus.Application.Common;
using Nimbus.Application.Common.Exceptions;
using Nimbus.Application.Common.Interfaces;
using Nimbus.Application.Features.Weather.Queries.GetMetarTaf;
namespace Nimbus.Application.Tests;

/// <summary>Issue #44: reads only. A provider that is down is a 502, never a half answer; a station with nothing published is a normal answer.</summary>
public class GetMetarTafHandlerTests
{
    private static GetMetarTafQueryHandler Handler(FakeWeather weather) => new(weather);

    [Test]
    public async Task ReturnsTheStationsMetarAndTaf_UppercasedAndTrimmed()
    {
        var weather = new FakeWeather(ProviderResult.Ok(new RawAviationWeather(
            "METAR EBAW 100850Z 08003KT 0400 FG Q1013\nMETAR EBBR 100850Z 09004KT 0600 FG Q1013\n",
            "TAF EBAW 100800Z 1009/1018 09006KT 0500 FG BKN001 \n  BECMG 1009/1011 6000 NSW SCT010\n")));

        var dto = await Handler(weather).Handle(new GetMetarTafQuery("  ebaw "), default);

        Assert.That(weather.AskedFor, Is.EqualTo("EBAW"));
        Assert.That(dto.Station, Is.EqualTo("EBAW"));
        Assert.That(dto.Metar, Is.EqualTo("EBAW 100850Z 08003KT 0400 FG Q1013"));
        Assert.That(dto.Taf, Is.EqualTo("TAF EBAW 100800Z 1009/1018 09006KT 0500 FG BKN001\n  BECMG 1009/1011 6000 NSW SCT010"));
    }

    [Test]
    public async Task AStationWithAMetarButNoTaf_ReturnsWhatThereIs()
    {
        var weather = new FakeWeather(ProviderResult.Ok(new RawAviationWeather("METAR EBOS 100850Z 09004KT 9999 Q1013", null)));

        var dto = await Handler(weather).Handle(new GetMetarTafQuery("EBOS"), default);

        Assert.That(dto.Metar, Is.EqualTo("EBOS 100850Z 09004KT 9999 Q1013"));
        Assert.That(dto.Taf, Is.Null);
    }

    [Test]
    public async Task AStationThatPublishesNothing_IsANormalAnswerWithBothPartsNull_NotAnError()
    {
        var weather = new FakeWeather(ProviderResult.Ok(new RawAviationWeather(null, null)));

        var dto = await Handler(weather).Handle(new GetMetarTafQuery("ZZZZ"), default);

        Assert.That((dto.Metar, dto.Taf), Is.EqualTo(((string?)null, (string?)null)));
    }

    [Test]
    public void AProviderFailure_IsAProviderUnavailableException_ThatTheApiAnswersWith502()
    {
        var weather = new FakeWeather(ProviderResult.Fail<RawAviationWeather>("METAR: the request timed out"));

        Assert.ThrowsAsync<ProviderUnavailableException>(() => Handler(weather).Handle(new GetMetarTafQuery("EBAW"), default));
    }

    [TestCase("EBAW", true)]
    [TestCase("ebaw", true)]
    [TestCase("EB1M", true)]
    [TestCase(" EBAW ", true)]
    [TestCase("", false)]
    [TestCase("  ", false)]
    [TestCase("EBA", false)]
    [TestCase("EBAWX", false)]
    [TestCase("EB-W", false)]
    [TestCase("EB W", false)]
    [TestCase("EBA&", false)]
    public void Validator_AcceptsOnlyFourLettersOrDigits(string code, bool valid)
    {
        Assert.That(new GetMetarTafQueryValidator().Validate(new GetMetarTafQuery(code)).IsValid, Is.EqualTo(valid));
    }

    private sealed class FakeWeather(ProviderResult<RawAviationWeather> result) : IAviationWeatherClient
    {
        public string? AskedFor { get; private set; }

        public Task<ProviderResult<RawAviationWeather>> GetRawAsync(string icaoCode, CancellationToken ct = default)
        {
            AskedFor = icaoCode;
            return Task.FromResult(result);
        }
    }
}
