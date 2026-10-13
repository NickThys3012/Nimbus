using Nimbus.Contracts.DTOs.Features.LaunchLocations;
using Nimbus.Domain.Entities;
namespace Nimbus.Contracts.Mappers;

public static class LaunchLocationMappers
{
    public static LaunchLocationDto MapToDto(this LaunchLocation l) =>
        new(l.Id, l.Name, l.IcaoCode, l.Latitude, l.Longitude, l.ElevationM, l.AirspaceNotes);

    /// <summary>Trims text, upper-cases the ICAO code and turns blank optional text into null.</summary>
    public static LaunchLocation MapToEntity(this LaunchLocationRequestDto r) => new()
    {
        Name = r.Name.Trim(),
        IcaoCode = string.IsNullOrWhiteSpace(r.IcaoCode) ? null : r.IcaoCode.Trim().ToUpperInvariant(),
        Latitude = r.Latitude,
        Longitude = r.Longitude,
        ElevationM = r.ElevationM,
        AirspaceNotes = string.IsNullOrWhiteSpace(r.AirspaceNotes) ? null : r.AirspaceNotes.Trim(),
    };
}
