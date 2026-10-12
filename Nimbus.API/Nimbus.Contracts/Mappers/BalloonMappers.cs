using Nimbus.Contracts.DTOs.Features.Balloons;
using Nimbus.Domain.Entities;
using Nimbus.Domain.Interfaces;
namespace Nimbus.Contracts.Mappers;

public static class BalloonMappers
{
    public static BalloonDto MapToDto(this Balloon b) => new(
        b.Id, b.Registration, b.Type, b.EnvelopeVolumeM3, b.MaxEnvelopeTemperatureC,
        b.EnvelopeWeightKg, b.BasketWeightKg, b.BurnerWeightKg, b.CylindersWeightKg, b.EmptyWeightKg);

    public static Balloon MapToEntity(this BalloonRequestDto r) => new()
    {
        Registration = Balloon.NormalizeRegistration(r.Registration),
        Type = (r.Type ?? "").Trim(),
        EnvelopeVolumeM3 = r.EnvelopeVolumeM3,
        MaxEnvelopeTemperatureC = r.MaxEnvelopeTemperatureC,
        EnvelopeWeightKg = r.EnvelopeWeightKg,
        BasketWeightKg = r.BasketWeightKg,
        BurnerWeightKg = r.BurnerWeightKg,
        CylindersWeightKg = r.CylindersWeightKg
    };

    public static BalloonUsageDto MapToDto(this BalloonUsage u) => new(
        u.FlightCount, u.RecentFlights.Select(f => new BalloonFlightDto(f.Label, f.Date)).ToList());
}
