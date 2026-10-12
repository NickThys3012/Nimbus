namespace Nimbus.Contracts.DTOs.Features.Balloons;

/// <summary>Editable balloon fields. Only the registration is required; the type is optional. The owner and the empty weight are never part of a request.</summary>
public record BalloonRequestDto(
    string Registration,
    string? Type,
    int EnvelopeVolumeM3,
    int MaxEnvelopeTemperatureC,
    decimal EnvelopeWeightKg,
    decimal BasketWeightKg,
    decimal BurnerWeightKg,
    decimal CylindersWeightKg);

public record BalloonDto(
    Guid Id,
    string Registration,
    string Type,
    int EnvelopeVolumeM3,
    int MaxEnvelopeTemperatureC,
    decimal EnvelopeWeightKg,
    decimal BasketWeightKg,
    decimal BurnerWeightKg,
    decimal CylindersWeightKg,
    decimal EmptyWeightKg);

/// <summary>A flight that uses a balloon, e.g. Label "VR 08 · Weelde".</summary>
public record BalloonFlightDto(string Label, DateOnly Date);

public record BalloonUsageDto(int FlightCount, IReadOnlyList<BalloonFlightDto> RecentFlights);
