using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Nimbus.Application.Common;
using Nimbus.Application.Common.Interfaces;
namespace Nimbus.Infrastructure.Weather;

/// <summary>
///     Open-Meteo forecast API for the hourly forecast and daily sunrise/sunset (issue #45): one request, wind in knots, in the
///     site's own timezone (<c>timezone=auto</c> derives it from the coordinates). When several models are asked for the
///     provider names every variable with the model as suffix, which is what lets each hour record its source model. Any provider
///     problem is a failed <see cref="ProviderResult{T}" /> and a logged warning, never an exception. Only the caller's own
///     cancellation propagates.
/// </summary>
public sealed class OpenMeteoHourlyForecastClient : IHourlyForecastClient
{
    private const string Variables = "temperature_2m,wind_speed_10m,wind_direction_10m,wind_gusts_10m,cape,precipitation,precipitation_probability";

    private readonly HttpClient _http;
    private readonly ILogger<OpenMeteoHourlyForecastClient> _logger;

    public OpenMeteoHourlyForecastClient(HttpClient http, ILogger<OpenMeteoHourlyForecastClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<ProviderResult<RawHourlyForecast>> GetAsync(double latitude, double longitude, IReadOnlyList<string> models, int days, CancellationToken ct = default)
    {
        var inv = CultureInfo.InvariantCulture;
        var modelList = string.Join(',', models);
        var url = $"forecast?latitude={latitude.ToString("R", inv)}&longitude={longitude.ToString("R", inv)}"
                  + $"&hourly={Variables}&daily=sunrise,sunset&models={Uri.EscapeDataString(modelList)}"
                  + $"&wind_speed_unit=kn&timezone=auto&forecast_days={days.ToString(inv)}";
        try
        {
            using var response = await _http.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
                return Failed(modelList, $"the provider answered {(int)response.StatusCode}", null);

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            var root = doc.RootElement;
            if (!root.TryGetProperty("hourly", out var hourly) || hourly.ValueKind != JsonValueKind.Object
                || !hourly.TryGetProperty("time", out var times) || times.ValueKind != JsonValueKind.Array)
                return Failed(modelList, "the reply had no hourly data", null);

            // A single model comes back with plain names, several with the model as suffix.
            string Key(string variable, string model) => models.Count == 1 ? variable : $"{variable}_{model}";

            var hours = new List<RawForecastHour>(times.GetArrayLength());
            for (var i = 0; i < times.GetArrayLength(); i++)
            {
                var byModel = new Dictionary<string, ForecastValues>();
                foreach (var model in models)
                    byModel[model] = new ForecastValues(
                        At(hourly, Key("temperature_2m", model), i), At(hourly, Key("wind_speed_10m", model), i),
                        At(hourly, Key("wind_direction_10m", model), i), At(hourly, Key("wind_gusts_10m", model), i),
                        At(hourly, Key("cape", model), i), At(hourly, Key("precipitation", model), i),
                        At(hourly, Key("precipitation_probability", model), i));
                hours.Add(new RawForecastHour(times[i].GetString() ?? "", byModel));
            }

            return ProviderResult.Ok(new RawHourlyForecast(
                root.TryGetProperty("timezone", out var tz) && tz.ValueKind == JsonValueKind.String ? tz.GetString()! : "GMT",
                root.TryGetProperty("utc_offset_seconds", out var off) && off.ValueKind == JsonValueKind.Number ? off.GetInt32() : 0,
                hours, ReadDays(root)));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException ex)
        {
            return Failed(modelList, "the request timed out", ex);
        }
        catch (HttpRequestException ex)
        {
            return Failed(modelList, "the provider could not be reached", ex);
        }
        catch (JsonException ex)
        {
            return Failed(modelList, "the reply was not valid JSON", ex);
        }
    }

    private static List<RawForecastDay> ReadDays(JsonElement root)
    {
        var days = new List<RawForecastDay>();
        if (!root.TryGetProperty("daily", out var daily) || daily.ValueKind != JsonValueKind.Object
            || !daily.TryGetProperty("time", out var dates) || dates.ValueKind != JsonValueKind.Array)
            return days;

        for (var i = 0; i < dates.GetArrayLength(); i++)
            days.Add(new RawForecastDay(dates[i].GetString() ?? "", Text(daily, "sunrise", i), Text(daily, "sunset", i)));
        return days;
    }

    private static string? Text(JsonElement obj, string name, int i) =>
        obj.TryGetProperty(name, out var arr) && arr.ValueKind == JsonValueKind.Array && i < arr.GetArrayLength() && arr[i].ValueKind == JsonValueKind.String
            ? arr[i].GetString()
            : null;

    private static double? At(JsonElement hourly, string name, int i) =>
        hourly.TryGetProperty(name, out var arr) && arr.ValueKind == JsonValueKind.Array && i < arr.GetArrayLength() && arr[i].ValueKind == JsonValueKind.Number
            ? arr[i].GetDouble()
            : null;

    private ProviderResult<RawHourlyForecast> Failed(string model, string reason, Exception? ex)
    {
        _logger.LogWarning(ex, "Open-Meteo hourly forecast fetch failed for model {Model}: {Reason}", model, reason);
        return ProviderResult.Fail<RawHourlyForecast>(reason);
    }
}
