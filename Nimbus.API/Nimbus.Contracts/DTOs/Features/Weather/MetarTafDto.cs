namespace Nimbus.Contracts.DTOs.Features.Weather;

/// <summary>
///     The current METAR and TAF for a station as raw text. Either is null when the provider publishes none for that
///     station (many small fields have no TAF); both null means nothing is published at all.
/// </summary>
public record MetarTafDto(string Station, string? Metar, string? Taf);
