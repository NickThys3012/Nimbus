using Microsoft.Extensions.Options;
using Nimbus.Infrastructure.Weather;
namespace Nimbus.Infrastructure.Http;

/// <summary>Adds the configured <c>apikey</c> parameter to every Open-Meteo request. Without a key it does nothing: the free endpoint takes none.</summary>
public sealed class OpenMeteoApiKeyHandler : DelegatingHandler
{
    private readonly string? _apiKey;

    public OpenMeteoApiKeyHandler(IOptions<OpenMeteoOptions> options)
    {
        _apiKey = options.Value.HasApiKey ? options.Value.ApiKey!.Trim() : null;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        if (_apiKey is not null && request.RequestUri is { } uri)
        {
            var builder = new UriBuilder(uri);
            builder.Query = (string.IsNullOrEmpty(builder.Query) ? "" : builder.Query.TrimStart('?') + "&") + "apikey=" + Uri.EscapeDataString(_apiKey);
            request.RequestUri = builder.Uri;
        }

        return base.SendAsync(request, ct);
    }
}
