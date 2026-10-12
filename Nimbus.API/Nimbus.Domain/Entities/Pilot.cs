using Nimbus.Domain.Entities.Base;
namespace Nimbus.Domain.Entities;

/// <summary>
///     A pilot (name and weight) a user can pick in flight preparation (issue #25). Owned by one
///     user; deleting it is a soft delete so flights that reference it keep resolving.
/// </summary>
public class Pilot : OwnedEntity
{
    public string Name { get; set; } = "";
    public decimal WeightKg { get; set; }
}
