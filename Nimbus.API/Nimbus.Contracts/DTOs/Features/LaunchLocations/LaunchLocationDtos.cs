namespace Nimbus.Contracts.DTOs.Features.LaunchLocations;

/// <summary>Editable launch-location fields. The owner is never part of a request.</summary>
public record LaunchLocationRequestDto(
    string Name,
    string? IcaoCode,
    decimal Latitude,
    decimal Longitude,
    decimal? ElevationM,
    string? AirspaceNotes);

public record LaunchLocationDto(
    Guid Id,
    string Name,
    string? IcaoCode,
    decimal Latitude,
    decimal Longitude,
    decimal? ElevationM,
    string? AirspaceNotes);
