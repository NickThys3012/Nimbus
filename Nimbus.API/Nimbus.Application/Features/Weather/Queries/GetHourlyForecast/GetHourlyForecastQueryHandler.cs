using MediatR;
using Nimbus.Application.Common.Exceptions;
using Nimbus.Application.Common.Interfaces;
using Nimbus.Contracts.DTOs.Features.Weather;
namespace Nimbus.Application.Features.Weather.Queries.GetHourlyForecast;

/// <summary>
///     Hourly forecast and sunrise/sunset for a place (issue #45). Reads only: nothing is stored. A seamless model is asked for
///     as its parts so each hour records the model it came from. A provider outage is a 502, which the UI shows as an empty state.
/// </summary>
public sealed class GetHourlyForecastQueryHandler : IRequestHandler<GetHourlyForecastQuery, HourlyForecastDto>
{
    private readonly IHourlyForecastClient _client;
    private readonly IForecastSettings _settings;

    public GetHourlyForecastQueryHandler(IHourlyForecastClient client, IForecastSettings settings)
    {
        _client = client;
        _settings = settings;
    }

    public async Task<HourlyForecastDto> Handle(GetHourlyForecastQuery request, CancellationToken ct)
    {
        var model = string.IsNullOrWhiteSpace(request.Model) ? _settings.DefaultModel : request.Model.Trim();
        var components = HourlyForecastModels.ComponentsOf(model);

        var result = await _client.GetAsync(request.Latitude, request.Longitude, components, _settings.Days, ct);
        if (!result.IsSuccess || result.Value is not { } raw)
            throw new ProviderUnavailableException("The forecast service");

        return HourlyForecastMapper.Map(model, components, raw);
    }
}
