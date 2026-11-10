namespace Nimbus.Contracts.DTOs.Features.Weather;

/// <summary>The wind by altitude for one UTC hour, lowest altitude first. Always complete: an hour missing a level is a gap, not a snapshot.</summary>
public record WindSnapshotDto(DateTime HourUtc, IReadOnlyList<WindProfileLevelDto> Levels);

/// <summary>
///     Hourly wind snapshots for a place between two UTC hours, for the trajectory simulator. The window is clamped to what the
///     model offers: <paramref name="FromUtc" /> / <paramref name="ToUtc" /> are what was served (null when nothing could be),
///     <paramref name="ShortfallHours" /> how many requested hours fall outside it. <paramref name="GapHoursUtc" /> are hours inside
///     the served window the model has no complete data for (left out, never zero-filled). <paramref name="ProviderAvailable" />
///     is false when the provider could not be reached: the series is then empty. <paramref name="Snapshots" /> is never null.
/// </summary>
public record WindSeriesDto(
    string Model,
    DateTime RequestedFromUtc,
    DateTime RequestedToUtc,
    DateTime? FromUtc,
    DateTime? ToUtc,
    int ShortfallHours,
    IReadOnlyList<DateTime> GapHoursUtc,
    bool ProviderAvailable,
    IReadOnlyList<WindSnapshotDto> Snapshots);
