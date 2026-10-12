namespace Nimbus.Contracts.DTOs.Features.Pilots;

/// <summary>Editable pilot fields. The owner is never part of a request.</summary>
public record PilotRequestDto(string Name, decimal WeightKg);

public record PilotDto(Guid Id, string Name, decimal WeightKg);
