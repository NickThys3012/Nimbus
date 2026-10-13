namespace Nimbus.Domain.Entities;

/// <summary>
///     A user's own profile defaults (issue #29): one row per user, created on first save. The values are
///     copied (snapshotted) onto a flight preparation when it is created; later edits never touch flights.
///     Both values are optional.
/// </summary>
public class UserProfile
{
    public Guid Id { get; set; }
    public Guid OwnerId { get; set; }
    public string? OperatorName { get; set; }

    /// <summary>Pilot-in-command weight in kg.</summary>
    public decimal? PicWeightKg { get; set; }

    public DateTime? UpdatedDate { get; set; }
}
