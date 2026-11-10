using Nimbus.Application.Features.Weather;
namespace Nimbus.Application.Tests;

/// <summary>Issue #44: the right station's report out of the provider's raw text. Samples are real aviationweather.gov replies.</summary>
public class MetarTafParserTests
{
    private const string EbawLine = "EBAW 100850Z 08003KT 0400 R11/0375N R29/0375N FG BKN001 01/01 Q1013 BECMG 3000 BR SCT002";
    private const string EbbrLine = "EBBR 100850Z 09004KT 060V120 0600 R25L/P2000N R25R/1600N R01/P2000N FG FEW001 BKN002 01/01 Q1013 BECMG 3000 BR";
    private const string MultiStation = $"METAR {EbawLine}\nMETAR {EbbrLine}\n";

    [Test]
    public void Metar_PicksTheRequestedStationOutOfAMultiLineResponse_WithoutTheLeadingWord()
    {
        Assert.That(MetarTafParser.SelectMetar(MultiStation, "EBBR"), Is.EqualTo(EbbrLine));
        Assert.That(MetarTafParser.SelectMetar(MultiStation, "EBAW"), Is.EqualTo(EbawLine));
    }

    [TestCase("ebbr")]
    [TestCase("EbBr")]
    public void Metar_MatchesTheIcaoCaseInsensitively(string icao)
    {
        Assert.That(MetarTafParser.SelectMetar(MultiStation, icao), Is.EqualTo(EbbrLine));
    }

    [Test]
    public void Metar_TakesTheFirstObservationOfAStation_BecauseTheProviderListsNewestFirst()
    {
        const string raw = "METAR EBBR 100850Z 09004KT 0600 FG 01/01 Q1013\nMETAR EBBR 100820Z 12004KT 0250 FG 01/01 Q1013\nMETAR EBBR 100750Z VRB02KT 0200 FG 01/01 Q1013\n";
        Assert.That(MetarTafParser.SelectMetar(raw, "EBBR"), Does.StartWith("EBBR 100850Z"));
    }

    [Test]
    public void Metar_AcceptsSpeciAndALineWithoutAReportWord()
    {
        Assert.That(MetarTafParser.SelectMetar("SPECI EBBR 100855Z 09004KT 0500 FG Q1013", "EBBR"), Is.EqualTo("EBBR 100855Z 09004KT 0500 FG Q1013"));
        Assert.That(MetarTafParser.SelectMetar("EBBR 100850Z 09004KT 9999 Q1013", "EBBR"), Is.EqualTo("EBBR 100850Z 09004KT 9999 Q1013"));
    }

    [Test]
    public void Metar_ASimilarCodeIsNotTheStation()
    {
        const string raw = "METAR EBAWX 100850Z 08003KT 9999 Q1013\nMETAR EBA 100850Z 08003KT 9999 Q1013\n";
        Assert.That(MetarTafParser.SelectMetar(raw, "EBAW"), Is.Null);
    }

    [Test]
    public void Metar_AStationNotInTheResponse_IsNull()
    {
        Assert.That(MetarTafParser.SelectMetar(MultiStation, "EBOS"), Is.Null);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("  \n  ")]
    public void NothingReturned_IsNull(string? raw)
    {
        Assert.That(MetarTafParser.SelectMetar(raw, "EBBR"), Is.Null);
        Assert.That(MetarTafParser.SelectTaf(raw, "EBBR"), Is.Null);
    }

    [Test]
    public void Metar_ToleratesWindowsLineEndingsAndStrayBlanks()
    {
        var raw = $"\r\n  METAR {EbawLine}  \r\n\r\nMETAR {EbbrLine}\r\n";
        Assert.That(MetarTafParser.SelectMetar(raw, "EBBR"), Is.EqualTo(EbbrLine));
    }

    private const string EbbrTaf =
        "TAF EBBR 100500Z 1006/1112 10004KT 0150 FG FZFG VV001 \n  TEMPO 1006/1009 3000 BR NSC \n  BECMG 1008/1010 7000 NSW NSC \n  BECMG 1019/1021 4000 -RA -RADZ SCT010 BKN012 \n  PROB40 \n  TEMPO 1021/1112 2000 RADZ BKN004 \n  PROB30 \n  TEMPO 1102/1110 0200 DZ FG BKN002\n";

    [Test]
    public void Taf_IsKeptAsItsFullMultiLineText_WithTrailingBlanksDropped_AndTheIndentKept()
    {
        var taf = MetarTafParser.SelectTaf(EbbrTaf, "EBBR")!;

        Assert.That(taf.Split('\n'), Has.Length.EqualTo(8));
        Assert.That(taf, Does.StartWith("TAF EBBR 100500Z 1006/1112"));
        Assert.That(taf.Split('\n')[1], Is.EqualTo("  TEMPO 1006/1009 3000 BR NSC"));
        Assert.That(taf, Does.EndWith("TEMPO 1102/1110 0200 DZ FG BKN002"));
        Assert.That(taf.Split('\n').Any(l => l != l.TrimEnd()), Is.False);
    }

    private const string ManyStations =
        "TAF AMD EDDF 100706Z 1007/1112 34004KT 9999 FEW014 BKN020 \n  BECMG 1007/1010 SCT020 \n  TEMPO 1022/1105 RADZ\n" +
        "TAF EGLL 100458Z 1006/1112 06006KT 6000 BKN005 \n  TEMPO 1006/1009 4000 BR BKN003 \n  PROB30 \n  TEMPO 1006/1007 0500 FG BKN001\n" +
        "TAF KJFK 100839Z 1009/1112 28010KT P6SM BKN045 BKN200 \n  FM101500 26013G21KT P6SM SCT045 SCT200\n";

    [Test]
    public void Taf_TakesOnlyTheRequestedStationsForecast_NotItsNeighboursAppended()
    {
        var egll = MetarTafParser.SelectTaf(ManyStations, "EGLL")!;
        Assert.That(egll, Does.StartWith("TAF EGLL 100458Z"));
        Assert.That(egll, Does.Contain("PROB30"));
        Assert.That(egll, Does.Not.Contain("EDDF").And.Not.Contain("KJFK"));

        var kjfk = MetarTafParser.SelectTaf(ManyStations, "KJFK")!;
        Assert.That(kjfk.Split('\n'), Has.Length.EqualTo(2));
    }

    [Test]
    public void Taf_AnAmendedForecastIsStillTheStationsForecast()
    {
        var eddf = MetarTafParser.SelectTaf(ManyStations, "eddf")!;
        Assert.That(eddf, Does.StartWith("TAF AMD EDDF 100706Z"));
        Assert.That(eddf.Split('\n'), Has.Length.EqualTo(3));
        Assert.That(MetarTafParser.SelectTaf("TAF COR EBBR 100500Z 1006/1112 10004KT 9999 NSC", "EBBR"), Does.StartWith("TAF COR EBBR"));
    }

    [Test]
    public void Taf_AStationWithoutOne_IsNull()
    {
        Assert.That(MetarTafParser.SelectTaf(ManyStations, "EBAW"), Is.Null);
        Assert.That(MetarTafParser.SelectTaf("TAF EBAWX 100500Z 1006/1112 10004KT 9999 NSC", "EBAW"), Is.Null);
    }
}
