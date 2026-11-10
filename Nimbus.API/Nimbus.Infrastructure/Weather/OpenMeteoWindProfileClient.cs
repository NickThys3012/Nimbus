using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Nimbus.Application.Common;
using Nimbus.Application.Common.Interfaces;
using Nimbus.Application.Features.Weather;
namespace Nimbus.Infrastructure.Weather;

/// <summary>
///     Open-Meteo forecast API for the wind profile (issue #46): one request for one hour, wind in knots, time in GMT. Any
///     provider problem is a failed <see cref="ProviderResult{T}" /> and a logged warning, never an exception. A variable the
///     model does not return stays null (it is never defaulted to zero). Only the caller's own cancellation propagates.
/// </summary>
public sealed class OpenMeteoWindProfileClient : IWindProfileClient, IWindSeriesClient
{
    private readonly HttpClient _http;
    private readonly ILogger<OpenMeteoWindProfileClient> _logger;

    public OpenMeteoWindProfileClient(HttpClient http, ILogger<OpenMeteoWindProfileClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<ProviderResult<RawWindProfile>> GetAsync(double latitude, double longitude, DateTime hourUtc, string model, CancellationToken ct = default)
    {
        var inv = CultureInfo.InvariantCulture;
        var hour = hourUtc.ToString("yyyy-MM-dd'T'HH:mm", inv);
        var url = $"forecast?latitude={latitude.ToString("R", inv)}&longitude={longitude.ToString("R", inv)}"
                  + $"&hourly={string.Join(',', WindProfileMapper.RequestedVariables)}"
                  + $"&models={Uri.EscapeDataString(model)}&wind_speed_unit=kn&timezone=GMT&start_hour={hour}&end_hour={hour}";
        try
        {
            using var response = await _http.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
                return Failed(model, $"the provider answered {(int)response.StatusCode}", null);

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            if (!doc.RootElement.TryGetProperty("hourly", out var hourly) || hourly.ValueKind != JsonValueKind.Object)
                return Failed(model, "the reply had no hourly data", null);

            var values = new Dictionary<string, double?>();
            foreach (var name in WindProfileMapper.RequestedVariables)
            {
                double? value = null;
                if (hourly.TryGetProperty(name, out var arr) && arr.ValueKind == JsonValueKind.Array && arr.GetArrayLength() > 0
                    && arr[0].ValueKind == JsonValueKind.Number)
                    value = arr[0].GetDouble();
                values[name] = value;
            }

            return ProviderResult.Ok(new RawWindProfile(values));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException ex)
        {
            return Failed(model, "the request timed out", ex);
        }
        catch (HttpRequestException ex)
        {
            return Failed(model, "the provider could not be reached", ex);
        }
        catch (JsonException ex)
        {
            return Failed(model, "the reply was not valid JSON", ex);
        }
    }

    public async Task<ProviderResult<RawWindSeries>> GetSeriesAsync(double latitude, double longitude, DateTime fromUtc, DateTime toUtc, string model, CancellationToken ct = default)
    {
        var inv = CultureInfo.InvariantCulture;
        string Stamp(DateTime t) => t.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm", inv);
        var url = $"forecast?latitude={latitude.ToString("R", inv)}&longitude={longitude.ToString("R", inv)}"
                  + $"&hourly={string.Join(',', WindProfileMapper.RequestedVariables)}"
                  + $"&models={Uri.EscapeDataString(model)}&wind_speed_unit=kn&timezone=GMT&start_hour={Stamp(fromUtc)}&end_hour={Stamp(toUtc)}";
        try
        {
            using var response = await _http.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
                return FailedSeries(model, $"the provider answered {(int)response.StatusCode}", null);

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            if (!doc.RootElement.TryGetProperty("hourly", out var hourly) || hourly.ValueKind != JsonValueKind.Object
                || !hourly.TryGetProperty("time", out var times) || times.ValueKind != JsonValueKind.Array)
                return FailedSeries(model, "the reply had no hourly data", null);

            var hours = new List<RawWindHour>(times.GetArrayLength());
            for (var i = 0; i < times.GetArrayLength(); i++)
            {
                if (!DateTime.TryParseExact(times[i].GetString(), "yyyy-MM-dd'T'HH:mm", inv, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var at))
                    continue;

                // A null (an hour past the model's horizon, a level it lacks) stays null: it is never turned into zero.
                var values = new Dictionary<string, double?>();
                foreach (var name in WindProfileMapper.RequestedVariables)
                    values[name] = hourly.TryGetProperty(name, out var arr) && arr.ValueKind == JsonValueKind.Array && i < arr.GetArrayLength()
                                   && arr[i].ValueKind == JsonValueKind.Number
                        ? arr[i].GetDouble()
                        : null;
                hours.Add(new RawWindHour(at, values));
            }

            return ProviderResult.Ok(new RawWindSeries(hours));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException ex)
        {
            return FailedSeries(model, "the request timed out", ex);
        }
        catch (HttpRequestException ex)
        {
            return FailedSeries(model, "the provider could not be reached", ex);
        }
        catch (JsonException ex)
        {
            return FailedSeries(model, "the reply was not valid JSON", ex);
        }
    }

    private ProviderResult<RawWindSeries> FailedSeries(string model, string reason, Exception? ex)
    {
        _logger.LogWarning(ex, "Open-Meteo wind series fetch failed for model {Model}: {Reason}", model, reason);
        return ProviderResult.Fail<RawWindSeries>(reason);
    }

    private ProviderResult<RawWindProfile> Failed(string model, string reason, Exception? ex)
    {
        _logger.LogWarning(ex, "Open-Meteo wind profile fetch failed for model {Model}: {Reason}", model, reason);
        return ProviderResult.Fail<RawWindProfile>(reason);
    }
}
