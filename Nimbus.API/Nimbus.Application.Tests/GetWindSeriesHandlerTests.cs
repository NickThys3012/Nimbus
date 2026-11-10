using Microsoft.Extensions.Caching.Memory;
using Nimbus.Application.Common;
using Nimbus.Application.Common.Exceptions;
using Nimbus.Application.Common.Interfaces;
using Nimbus.Application.Features.Weather;
using Nimbus.Application.Features.Weather.Queries.GetWindSeries;
namespace Nimbus.Application.Tests;

/// <summary>Issue #47: the window is clamped to the model's horizon with the shortfall reported, failures and nulls never become zeros or exceptions, and re-runs hit the cache.</summary>
public class GetWindSeriesHandlerTests
{
    private static readonly DateTime Now = new(2026, 11, 10, 9, 0, 0, DateTimeKind.Utc);

    private sealed class FakeClient(Func<DateTime, DateTime, ProviderResult<RawWindSeries>> respond) : IWindSeriesClient
    {
        public int Calls; public DateTime? From; public DateTime? To; public string? Model;
        public Task<ProviderResult<RawWindSeries>> GetSeriesAsync(double latitude, double longitude, DateTime fromUtc, DateTime toUtc, string model, CancellationToken ct = default)
        {
            Calls++; From = fromUtc; To = toUtc; Model = model;
            return Task.FromResult(respond(fromUtc, toUtc));
        }
    }

    private sealed class Settings : IForecastSettings
    {
        public string DefaultModel => "icon_seamless";
        public int Days => 3;
        public int SeriesCacheMinutes => 60;
    }

    private sealed class Clock(DateTime now) : TimeProvider { public override DateTimeOffset GetUtcNow() => new(now, TimeSpan.Zero); }

    private static Dictionary<string, double?> Full(params string[] without) =>
        WindProfileMapper.RequestedVariables.ToDictionary(n => n, n => without.Contains(n) ? (double?)null : 8);

    private static ProviderResult<RawWindSeries> Hours(DateTime from, DateTime to, Func<DateTime, Dictionary<string, double?>>? values = null)
    {
        var list = new List<RawWindHour>();
        for (var h = from; h <= to; h = h.AddHours(1)) list.Add(new RawWindHour(h, (values ?? (_ => Full()))(h)));
        return ProviderResult.Ok(new RawWindSeries(list));
    }

    private static GetWindSeriesQueryHandler Handler(FakeClient c, IMemoryCache? cache = null) =>
        new(c, cache ?? new MemoryCache(new MemoryCacheOptions()), new Settings(), new Clock(Now.AddMinutes(41)));

    [Test]
    public async Task AWindowInsideTheHorizon_ReturnsOneCompleteSnapshotPerHour_WithNoShortfall()
    {
        var client = new FakeClient((f, t) => Hours(f, t));

        var dto = await Handler(client).Handle(new GetWindSeriesQuery(51.1568, 4.0439, Now.AddHours(2), Now.AddHours(5)), default);

        Assert.That(dto.Snapshots.Select(s => s.HourUtc), Is.EqualTo(new[] { Now.AddHours(2), Now.AddHours(3), Now.AddHours(4), Now.AddHours(5) }));
        Assert.That(dto.Snapshots.All(s => s.Levels.Count == 10), Is.True);
        Assert.That(dto.ShortfallHours, Is.EqualTo(0));
        Assert.That(dto.GapHoursUtc, Is.Empty);
        Assert.That(dto.ProviderAvailable, Is.True);
        Assert.That(client.Model, Is.EqualTo("icon_seamless"));
    }

    [TestCase("icon_seamless", 96)]
    [TestCase("icon_d2", 59)]
    [TestCase("meteofrance_arome_france", 56)]
    public async Task AWindowPastTheModelsHorizon_IsClamped_AndTheShortfallReported(string model, int horizon)
    {
        var client = new FakeClient((f, t) => Hours(f, t));

        var dto = await Handler(client).Handle(new GetWindSeriesQuery(51, 4, Now.AddHours(40), Now.AddHours(120), model), default);

        Assert.That(dto.ToUtc, Is.EqualTo(Now.AddHours(horizon)));
        Assert.That(client.To, Is.EqualTo(Now.AddHours(horizon)));
        Assert.That(dto.FromUtc, Is.EqualTo(Now.AddHours(40)));
        Assert.That(dto.ShortfallHours, Is.EqualTo(120 - horizon));
        Assert.That(dto.RequestedToUtc, Is.EqualTo(Now.AddHours(120)));
    }

    [Test]
    public async Task HoursInThePast_AreClampedToNow_AndCountedInTheShortfall()
    {
        var client = new FakeClient((f, t) => Hours(f, t));

        var dto = await Handler(client).Handle(new GetWindSeriesQuery(51, 4, Now.AddHours(-3), Now.AddHours(1)), default);

        Assert.That(dto.FromUtc, Is.EqualTo(Now));
        Assert.That(dto.ShortfallHours, Is.EqualTo(3));
    }

    [Test]
    public async Task AWindowEntirelyBeyondTheHorizon_IsAnEmptySeries_WithoutCallingTheProvider()
    {
        var client = new FakeClient((f, t) => Hours(f, t));

        var dto = await Handler(client).Handle(new GetWindSeriesQuery(51, 4, Now.AddHours(100), Now.AddHours(104), "icon_d2"), default);

        Assert.That(dto.Snapshots, Is.Empty);
        Assert.That(dto.FromUtc, Is.Null);
        Assert.That(dto.ShortfallHours, Is.EqualTo(5));
        Assert.That(client.Calls, Is.EqualTo(0));
    }

    [Test]
    public async Task AFailedProvider_IsAnEmptySeries_NotAnExceptionOrNull()
    {
        var client = new FakeClient((_, _) => ProviderResult.Fail<RawWindSeries>("down"));

        var dto = await Handler(client).Handle(new GetWindSeriesQuery(51, 4, Now, Now.AddHours(3)), default);

        Assert.That(dto.Snapshots, Is.Not.Null.And.Empty);
        Assert.That(dto.GapHoursUtc, Is.Not.Null);
        Assert.That(dto.ProviderAvailable, Is.False);
        Assert.That(dto.ShortfallHours, Is.EqualTo(4));
    }

    [Test]
    public async Task AnEmptyReply_IsAllGaps_NotAnError()
    {
        var client = new FakeClient((_, _) => ProviderResult.Ok(new RawWindSeries([])));

        var dto = await Handler(client).Handle(new GetWindSeriesQuery(51, 4, Now, Now.AddHours(2)), default);

        Assert.That(dto.Snapshots, Is.Empty);
        Assert.That(dto.GapHoursUtc, Has.Count.EqualTo(3));
        Assert.That(dto.ProviderAvailable, Is.True);
    }

    [Test]
    public async Task AnHourMissingALevel_IsAReportedGap_NeverZeroFilled()
    {
        var gap = Now.AddHours(2);
        var client = new FakeClient((f, t) => Hours(f, t, h => h == gap ? Full("wind_speed_950hPa") : Full()));

        var dto = await Handler(client).Handle(new GetWindSeriesQuery(51, 4, Now, Now.AddHours(3)), default);

        Assert.That(dto.GapHoursUtc, Is.EqualTo(new[] { gap }));
        Assert.That(dto.Snapshots.Select(s => s.HourUtc), Does.Not.Contain(gap));
        Assert.That(dto.Snapshots, Has.Count.EqualTo(3));
        Assert.That(dto.Snapshots.SelectMany(s => s.Levels).All(l => l.SpeedKt > 0), Is.True);
    }

    [Test]
    public async Task ARepeatedRequest_IsServedFromTheCache_ButAnotherModelOrWindowIsNot()
    {
        var client = new FakeClient((f, t) => Hours(f, t));
        var handler = Handler(client);
        var q = new GetWindSeriesQuery(51.1568, 4.0439, Now, Now.AddHours(3));

        await handler.Handle(q, default);
        await handler.Handle(q, default);
        Assert.That(client.Calls, Is.EqualTo(1));

        await handler.Handle(q with { Model = "icon_eu" }, default);
        await handler.Handle(q with { ToUtc = Now.AddHours(4) }, default);
        Assert.That(client.Calls, Is.EqualTo(3));
    }

    [Test]
    public async Task AFailedFetch_IsNotCached()
    {
        var fail = true;
        var client = new FakeClient((f, t) => fail ? ProviderResult.Fail<RawWindSeries>("down") : Hours(f, t));
        var handler = Handler(client);
        var q = new GetWindSeriesQuery(51, 4, Now, Now.AddHours(1));

        await handler.Handle(q, default);
        fail = false;
        var dto = await handler.Handle(q, default);

        Assert.That(dto.Snapshots, Has.Count.EqualTo(2));
        Assert.That(client.Calls, Is.EqualTo(2));
    }

    [Test]
    public void AModelWithoutAllLevels_IsRejectedBeforeAnyFetch()
    {
        var client = new FakeClient((f, t) => Hours(f, t));

        Assert.ThrowsAsync<ValidationException>(() => Handler(client).Handle(new GetWindSeriesQuery(51, 4, Now, Now.AddHours(1), "gfs_global"), default));
        Assert.That(client.Calls, Is.EqualTo(0));
    }

    [Test]
    public void Validator_RejectsABackwardsOrTooLongWindow_AndBadInput()
    {
        var v = new GetWindSeriesQueryValidator();

        Assert.That(v.Validate(new GetWindSeriesQuery(51, 4, Now, Now.AddHours(-1))).IsValid, Is.False);
        Assert.That(v.Validate(new GetWindSeriesQuery(51, 4, Now, Now.AddDays(8))).IsValid, Is.False);
        Assert.That(v.Validate(new GetWindSeriesQuery(91, 4, Now, Now.AddHours(1))).IsValid, Is.False);
        Assert.That(v.Validate(new GetWindSeriesQuery(51, 4, Now, Now.AddHours(1), "metno_seamless")).IsValid, Is.False);
        Assert.That(v.Validate(new GetWindSeriesQuery(51, 4, Now, Now.AddHours(1))).IsValid, Is.True);
    }
}
