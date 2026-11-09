using MediatR;
using Nimbus.Contracts.DTOs.Features.LiftCalculation;
using Nimbus.Domain.Services;
namespace Nimbus.Application.Features.LiftCalculation.Queries.CalculateTotalLift;

public sealed class CalculateTotalLiftQueryHandler : IRequestHandler<CalculateTotalLiftQuery, TotalLiftDto>
{
    public Task<TotalLiftDto> Handle(CalculateTotalLiftQuery request, CancellationToken ct)
    {
        var lift = LiftCalculator.CalculateTotalLiftKg(
            (double)request.MaxAltitudeMeters,
            (double)request.SiteElevationMeters,
            (double)request.AmbientTemperatureC,
            (double)request.InternalTemperatureC,
            (double)request.EnvelopeVolumeM3);
        return Task.FromResult(new TotalLiftDto(decimal.Round((decimal)lift, 1)));
    }
}
