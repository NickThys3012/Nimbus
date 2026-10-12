using Nimbus.Domain.Entities.Base;
namespace Nimbus.Domain.Entities;

/// <summary>
///     A balloon a pilot flies (issue #24). Owned by one user; deleting it is a soft delete so a
///     flight that references it keeps resolving (see docs/balloons.md).
/// </summary>
public class Balloon : OwnedEntity
{
    /// <summary>Required and unique per owner (case-insensitive, stored upper-case).</summary>
    public string Registration { get; set; } = "";
    public string Type { get; set; } = "";
    public int EnvelopeVolumeM3 { get; set; }
    public int MaxEnvelopeTemperatureC { get; set; }

    // OFP reference weights, in kg.
    public decimal EnvelopeWeightKg { get; set; }
    public decimal BasketWeightKg { get; set; }
    public decimal BurnerWeightKg { get; set; }
    public decimal CylindersWeightKg { get; set; }

    /// <summary>Derived, never stored or typed in: the sum of the reference weights.</summary>
    public decimal EmptyWeightKg => EnvelopeWeightKg + BasketWeightKg + BurnerWeightKg + CylindersWeightKg;

    public static string NormalizeRegistration(string registration) => registration.Trim().ToUpperInvariant();
}
