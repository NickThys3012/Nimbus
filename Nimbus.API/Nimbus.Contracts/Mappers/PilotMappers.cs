using Nimbus.Contracts.DTOs.Features.Pilots;
using Nimbus.Domain.Entities;
namespace Nimbus.Contracts.Mappers;

public static class PilotMappers
{
    public static PilotDto MapToDto(this Pilot p) => new(p.Id, p.Name, p.WeightKg);

    public static Pilot MapToEntity(this PilotRequestDto r) => new() { Name = r.Name.Trim(), WeightKg = r.WeightKg };
}
