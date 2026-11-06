namespace Nimbus.Domain.Enums;

/// <summary>
///     The answer to a pre-flight safety check that can genuinely be not applicable (issue #34).
///     Replaces the POC's free strings ("JA" / "NEE" / "NVT"). The default (value 0) is
///     <see cref="No" />: an unchecked item is never silently recorded as done.
/// </summary>
public enum ChecklistAnswer
{
    /// <summary>Not done / not confirmed. The safe, unchecked default (POC "NEE").</summary>
    No = 0,

    /// <summary>Done / confirmed (POC "JA").</summary>
    Yes = 1,

    /// <summary>Genuinely not applicable to this flight (POC "NVT").</summary>
    NotApplicable = 2
}
