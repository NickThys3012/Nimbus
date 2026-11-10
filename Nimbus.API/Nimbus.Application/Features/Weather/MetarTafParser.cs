using System.Text;
namespace Nimbus.Application.Features.Weather;

/// <summary>
///     Picks one station's report out of the provider's raw text. A response can carry several stations (and several
///     observations of one station, newest first), so the station is chosen by matching the ICAO code at the start of the
///     report, ignoring case; nothing else in the response is trusted to be "the" report.
/// </summary>
public static class MetarTafParser
{
    /// <summary>
    ///     The newest METAR/SPECI line of the station, without the leading "METAR"/"SPECI" word the provider adds (the form
    ///     shows the report itself), or null when the station has none.
    /// </summary>
    public static string? SelectMetar(string? raw, string icao)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        foreach (var line in Lines(raw))
        {
            var report = StripReportType(line);
            if (StartsWithStation(report, icao))
                return report;
        }

        return null;
    }

    /// <summary>
    ///     The station's full TAF as multi-line text. A TAF starts at a line beginning "TAF" and continues over the indented
    ///     lines that follow; "TAF AMD" and "TAF COR" are the same forecast amended or corrected. Line endings are
    ///     normalised and trailing blanks dropped; the continuation lines keep their indent so it reads like the original.
    /// </summary>
    public static string? SelectTaf(string? raw, string icao)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        var block = new List<string>();
        var matching = false;
        foreach (var rawLine in raw.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            var line = rawLine.TrimEnd();
            if (line.Length == 0) continue;

            var startsReport = !char.IsWhiteSpace(line[0]);
            if (startsReport)
            {
                if (matching) break; // the next report begins: the station's TAF is complete
                matching = StartsWithStation(StripTafHeader(line), icao);
            }

            if (matching) block.Add(line);
        }

        return block.Count == 0 ? null : string.Join('\n', block);
    }

    private static IEnumerable<string> Lines(string raw) =>
        raw.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0);

    private static string StripReportType(string line)
    {
        foreach (var type in new[] { "METAR", "SPECI" })
        {
            if (line.StartsWith(type + " ", StringComparison.OrdinalIgnoreCase))
                return line[(type.Length + 1)..].TrimStart();
        }

        return line;
    }

    /// <summary>"TAF EBAW …", "TAF AMD EBAW …" and "TAF COR EBAW …" all reduce to "EBAW …".</summary>
    private static string StripTafHeader(string line)
    {
        var rest = line;
        if (!rest.StartsWith("TAF", StringComparison.OrdinalIgnoreCase)) return rest;
        rest = rest[3..].TrimStart();
        foreach (var amendment in new[] { "AMD", "COR" })
        {
            if (rest.StartsWith(amendment + " ", StringComparison.OrdinalIgnoreCase))
                rest = rest[(amendment.Length + 1)..].TrimStart();
        }

        return rest;
    }

    /// <summary>The report starts with the station code as a whole word, so EBAW does not match EBAWX or a longer token.</summary>
    private static bool StartsWithStation(string report, string icao) =>
        report.StartsWith(icao, StringComparison.OrdinalIgnoreCase) && (report.Length == icao.Length || char.IsWhiteSpace(report[icao.Length]));
}
