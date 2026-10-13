using Nimbus.Domain.Entities.Base;
namespace Nimbus.Domain.Entities;

/// <summary>
///     A field a user launches from (issue #26): coordinates drive weather and sunrise calculations and the
///     ICAO code, where present, drives the METAR/TAF lookup. Owned by one user; deleting it is a soft delete
///     so flights that reference it keep resolving.
/// </summary>
public class LaunchLocation : OwnedEntity
{
    public string Name { get; set; } = "";

    /// <summary>Four-character ICAO code, upper case; null when the field has none.</summary>
    public string? IcaoCode { get; set; }

    /// <summary>Decimal degrees, -90 to 90.</summary>
    public decimal Latitude { get; set; }

    /// <summary>Decimal degrees, -180 to 180.</summary>
    public decimal Longitude { get; set; }

    /// <summary>Metres above mean sea level.</summary>
    public decimal ElevationM { get; set; }

    public string? AirspaceNotes { get; set; }
}
