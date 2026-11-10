using System.Globalization;
using FluentValidation.Results;
using MediatR;
using Microsoft.Extensions.Caching.Memory;
using Nimbus.Application.Common.Exceptions;
using Nimbus.Application.Common.Interfaces;
using Nimbus.Contracts.DTOs.Features.Weather;
namespace Nimbus.Application.Features.Weather.Queries.GetWindSeries;

/// <summary>
///     Hourly wind snapshots across the pressure levels for the trajectory simulator (issue #47). Reads only. The window is
///     clamped to what the model offers from now (no past hours, no hours beyond its horizon) and the shortfall is reported. A
///     provider that fails yields an empty series (never null, never an exception); an hour a model has no complete data for is
///     a reported gap, never zeros. A successful fetch is cached per model, place and window so a re-run does not refetch;
///     the model is in the key because the models update on different cycles.
/// </summary>
public sealed class GetWindSeriesQueryHandler : IRequestHandler<GetWindSeriesQuery, WindSeriesDto>
{
    private const int DefaultHorizonHours = 96;

    /// <summary>Hours ahead each short-range model covers (issue #47). Every other supported model reaches 96 h.</summary>
    private static readonly IReadOnlyDictionary<string, int> HorizonHours = new Dictionary<string, int>
    {
        ["icon_d2"] = 59,
        ["meteofrance_arome_france"] = 56,
        ["ukmo_uk_deterministic_2km"] = 59
    };

    private readonly IWindSeriesClient _client;
    private readonly IMemoryCache _cache;
    private readonly IForecastSettings _settings;
    private readonly TimeProvider _time;

    public GetWindSeriesQueryHandler(IWindSeriesClient client, IMemoryCache cache, IForecastSettings settings, TimeProvider time)
    {
        _client = client;
        _cache = cache;
        _settings = settings;
        _time = time;
    }

    public static int HorizonOf(string model) => HorizonHours.TryGetValue(model, out var h) ? h : DefaultHorizonHours;

    public async Task<WindSeriesDto> Handle(GetWindSeriesQuery request, CancellationToken ct)
    {
        var model = string.IsNullOrWhiteSpace(request.Model) ? _settings.DefaultModel : request.Model.Trim();
        if (!WindProfileMapper.SupportedModels.Contains(model))
            throw new ValidationException([new ValidationFailure("Model", $"Model '{model}' lacks pressure levels or low-altitude winds, so it cannot drive a simulation.")]);

        var from = FloorToHour(request.FromUtc);
        var to = FloorToHour(request.ToUtc);
        var now = FloorToHour(_time.GetUtcNow().UtcDateTime);

        // Only hours from now to the model's horizon exist.
        var servedFrom = from > now ? from : now;
        var horizon = now.AddHours(HorizonOf(model));
        var servedTo = to < horizon ? to : horizon;
        var requestedHours = HoursBetween(from, to);

        if (servedFrom > servedTo)
            return Empty(model, from, to, requestedHours, providerAvailable: true);

        var key = string.Create(CultureInfo.InvariantCulture, $"wind-series|{model}|{request.Latitude:F2}|{request.Longitude:F2}|{servedFrom:O}|{servedTo:O}");
        if (!_cache.TryGetValue(key, out RawWindSeries? raw) || raw is null)
        {
            var result = await _client.GetSeriesAsync(request.Latitude, request.Longitude, servedFrom, servedTo, model, ct);
            if (!result.IsSuccess || result.Value is not { } fetched)
                return Empty(model, from, to, requestedHours, providerAvailable: false);

            raw = fetched;
            _cache.Set(key, raw, TimeSpan.FromMinutes(_settings.SeriesCacheMinutes));
        }

        var byHour = raw.Hours.GroupBy(h => FloorToHour(h.HourUtc)).ToDictionary(g => g.Key, g => g.First());
        var snapshots = new List<WindSnapshotDto>();
        var gaps = new List<DateTime>();
        for (var hour = servedFrom; hour <= servedTo; hour = hour.AddHours(1))
        {
            if (byHour.TryGetValue(hour, out var raw1) && WindProfileMapper.Map(raw1.Values) is { Missing.Count: 0 } mapping)
                snapshots.Add(new WindSnapshotDto(hour, mapping.Levels));
            else
                gaps.Add(hour);
        }

        return new WindSeriesDto(model, from, to, servedFrom, servedTo, requestedHours - HoursBetween(servedFrom, servedTo), gaps, true, snapshots);
    }

    private static WindSeriesDto Empty(string model, DateTime from, DateTime to, int requestedHours, bool providerAvailable) =>
        new(model, from, to, null, null, requestedHours, [], providerAvailable, []);

    private static int HoursBetween(DateTime from, DateTime to) => (int)(to - from).TotalHours + 1;

    private static DateTime FloorToHour(DateTime t)
    {
        var u = t.ToUniversalTime();
        return new DateTime(u.Year, u.Month, u.Day, u.Hour, 0, 0, DateTimeKind.Utc);
    }
}
