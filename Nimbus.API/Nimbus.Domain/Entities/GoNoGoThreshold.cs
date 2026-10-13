namespace Nimbus.Domain.Entities;

/// <summary>
///     A user's personal Go/No-Go limits (issue #27): one row per user. A row whose
///     <see cref="OwnerId" /> is null is the global default: read-only fallback for users without
///     their own row, never writable through the user endpoints. Not a soft-deleted entity:
///     resetting to defaults removes the row.
/// </summary>
public class GoNoGoThreshold
{
    public Guid Id { get; set; }
    public Guid? OwnerId { get; set; }

    /// <summary>Surface wind in knots: caution from <see cref="WindYellowKt" />, no-go from <see cref="WindRedKt" />.</summary>
    public decimal WindYellowKt { get; set; }
    public decimal WindRedKt { get; set; }

    /// <summary>Visibility in km: caution at or below <see cref="VisibilityYellowKm" />, no-go at or below <see cref="VisibilityRedKm" />.</summary>
    public decimal VisibilityYellowKm { get; set; }
    public decimal VisibilityRedKm { get; set; }

    /// <summary>CAPE in J/kg.</summary>
    public decimal CapeYellow { get; set; }
    public decimal CapeRed { get; set; }

    public DateTime? UpdatedDate { get; set; }
}
