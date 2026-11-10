using System.Text;
using Microsoft.Extensions.Logging;
using Nimbus.Application.Common;
using Nimbus.Application.Common.Interfaces;
namespace Nimbus.Infrastructure.Weather;

/// <summary>
///     aviationweather.gov Data API, raw format (issue #44). One request each for METAR and TAF, run side by side. Anything
///     the provider does wrong (unreachable, too slow, an error status, an oversized reply) is a failed
///     <see cref="ProviderResult{T}" /> and a logged warning, never an exception; "no data" (204, or an empty body) is a
///     success with nothing in it. Only the caller's own cancellation propagates.
/// </summary>
public sealed class AviationWeatherClient : IAviationWeatherClient
{
    /// <summary>A reply this large is not a METAR or a TAF; reading it would only waste memory.</summary>
    private const int MaxResponseChars = 256 * 1024;

    private readonly HttpClient _http;
    private readonly ILogger<AviationWeatherClient> _logger;

    public AviationWeatherClient(HttpClient http, ILogger<AviationWeatherClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<ProviderResult<RawAviationWeather>> GetRawAsync(string icaoCode, CancellationToken ct = default)
    {
        var id = Uri.EscapeDataString(icaoCode);
        var metar = GetTextAsync($"metar?ids={id}&format=raw", icaoCode, "METAR", ct);
        var taf = GetTextAsync($"taf?ids={id}&format=raw", icaoCode, "TAF", ct);
        var results = await Task.WhenAll(metar, taf);

        var failed = results.FirstOrDefault(r => !r.IsSuccess);
        return failed is not null
            ? ProviderResult.Fail<RawAviationWeather>(failed.Error ?? "The provider request failed.")
            : ProviderResult.Ok(new RawAviationWeather(results[0].Value, results[1].Value));
    }

    private async Task<ProviderResult<string?>> GetTextAsync(string relativeUrl, string icao, string kind, CancellationToken ct)
    {
        try
        {
            using var response = await _http.GetAsync(relativeUrl, HttpCompletionOption.ResponseHeadersRead, ct);
            if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
                return ProviderResult.Ok<string?>(null);
            if (!response.IsSuccessStatusCode)
                return Failed(icao, kind, $"the provider answered {(int)response.StatusCode}", null);

            var text = await ReadLimitedAsync(response, ct);
            if (text is null)
                return Failed(icao, kind, "the reply was unreasonably large", null);
            return ProviderResult.Ok<string?>(string.IsNullOrWhiteSpace(text) ? null : text);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw; // the caller gave up: not the provider's failure
        }
        catch (OperationCanceledException ex)
        {
            return Failed(icao, kind, "the request timed out", ex);
        }
        catch (HttpRequestException ex)
        {
            return Failed(icao, kind, "the provider could not be reached", ex);
        }
    }

    private ProviderResult<string?> Failed(string icao, string kind, string reason, Exception? ex)
    {
        _logger.LogWarning(ex, "Aviation weather {Kind} fetch failed for {Icao}: {Reason}", kind, icao, reason);
        return ProviderResult.Fail<string?>($"{kind}: {reason}");
    }

    /// <summary>The body as text, or null when it is longer than <see cref="MaxResponseChars" />.</summary>
    private static async Task<string?> ReadLimitedAsync(HttpResponseMessage response, CancellationToken ct)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var sb = new StringBuilder();
        var buffer = new char[4096];
        int read;
        while ((read = await reader.ReadAsync(buffer.AsMemory(), ct)) > 0)
        {
            sb.Append(buffer, 0, read);
            if (sb.Length > MaxResponseChars) return null;
        }

        return sb.ToString();
    }
}
