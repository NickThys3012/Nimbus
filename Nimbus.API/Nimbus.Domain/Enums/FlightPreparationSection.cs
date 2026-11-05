namespace Nimbus.Domain.Enums;

/// <summary>
///     Which of the nine dossier sections an uploaded <c>FlightPreparationImage</c> belongs to
///     (issue #31). Drives both the section an image is shown under and, combined with
///     <c>Order</c>, how images within a section are sorted.
/// </summary>
public enum FlightPreparationSection
{
    /// <summary>01 — General flight information.</summary>
    General,

    /// <summary>02 — Meteo.</summary>
    Meteo,

    /// <summary>03 — Airspace &amp; NOTAMs.</summary>
    Airspace,

    /// <summary>04 — Checklists.</summary>
    Checklists,

    /// <summary>05 — Load calculation.</summary>
    LoadCalculation,

    /// <summary>06 — Route &amp; trajectory.</summary>
    Route,

    /// <summary>07 — Balloon bulletin.</summary>
    Bulletin,

    /// <summary>08 — Operational Flight Plan.</summary>
    Ofp,

    /// <summary>09 — Passenger briefing.</summary>
    PaxBriefing
}
