using MediatR;
using Nimbus.Application.Common.Exceptions;
using Nimbus.Application.Common.Interfaces;
using Nimbus.Contracts.DTOs.Features.Weather;
namespace Nimbus.Application.Features.Weather.Queries.GetMetarTaf;

/// <summary>
///     Current METAR and TAF for a station (issue #44). Reads only: nothing is stored, so a provider failure can never
///     clear or change anything the pilot already has. A provider that is down is a 502; a station with nothing
///     published is a normal answer with both parts null.
/// </summary>
public sealed class GetMetarTafQueryHandler : IRequestHandler<GetMetarTafQuery, MetarTafDto>
{
    private readonly IAviationWeatherClient _weather;

    public GetMetarTafQueryHandler(IAviationWeatherClient weather)
    {
        _weather = weather;
    }

    public async Task<MetarTafDto> Handle(GetMetarTafQuery request, CancellationToken ct)
    {
        var icao = request.IcaoCode.Trim().ToUpperInvariant();
        var result = await _weather.GetRawAsync(icao, ct);
        if (!result.IsSuccess || result.Value is not { } raw)
            throw new ProviderUnavailableException("The aviation weather service");

        return new MetarTafDto(icao, MetarTafParser.SelectMetar(raw.MetarText, icao), MetarTafParser.SelectTaf(raw.TafText, icao));
    }
}
