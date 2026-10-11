namespace Nimbus.Domain.Entities.Base;

public abstract class OwnedEntity : BaseEntity, IOwnedEntity
{
    public Guid OwnerId { get; set; }
}
