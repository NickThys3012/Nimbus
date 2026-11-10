using FluentValidation.Results;
using MediatR;
using Nimbus.Application.Common.Exceptions;
using Nimbus.Application.Common.Interfaces;
using Nimbus.Contracts.DTOs.Features.Weather;
namespace Nimbus.Application.Features.Weather.Queries.GetWindProfile;

/// <summary>
///     Wind profile for a place and hour (issue #46). Reads only: nothing is stored, so a fetch can never overwrite what the
///     pilot typed; the client decides (and confirms) what to do with the levels. A provider outage is a 502. A model that
///     lacks any required level is a 422 naming the missing levels, never a profile with zeros.
/// </summary>
public sealed class GetWindProfileQueryHandler : IRequestHandler<GetWindProfileQuery, WindProfileDto>
{
    private readonly IWindProfileClient _client;
    private readonly TimeProvider _time;

    public GetWindProfileQueryHandler(IWindProfileClient client, TimeProvider time)
    {
        _client = client;
        _time = time;
    }

    public async Task<WindProfileDto> Handle(GetWindProfileQuery request, CancellationToken ct)
    {
        var model = string.IsNullOrWhiteSpace(request.Model) ? WindProfileMapper.DefaultModel : request.Model.Trim();
        var at = request.HourUtc?.ToUniversalTime() ?? _time.GetUtcNow().UtcDateTime;
        var hour = new DateTime(at.Year, at.Month, at.Day, at.Hour, 0, 0, DateTimeKind.Utc);

        var result = await _client.GetAsync(request.Latitude, request.Longitude, hour, model, ct);
        if (!result.IsSuccess || result.Value is not { } raw)
            throw new ProviderUnavailableException("The forecast service");

        var mapping = WindProfileMapper.Map(raw.Values);
        if (mapping.Missing.Count > 0)
            throw new ValidationException([new ValidationFailure("Model",
                $"Model '{model}' returned no data for: {string.Join(", ", mapping.Missing)} at that place and hour. Choose another model or hour.")]);

        return new WindProfileDto(model, hour, mapping.Levels);
    }
}
