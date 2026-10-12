using Microsoft.Extensions.Logging;
using Nimbus.Application.Common.Ownership;
using Nimbus.Domain.Entities;
using Nimbus.Domain.Interfaces;
namespace Nimbus.Application.Features.Pilots;

public interface IPilotService : IOwnedEntityService<Pilot> {}

/// <summary>Owner-scoped pilot operations (see docs/pilots.md).</summary>
public sealed class PilotService : OwnedEntityService<Pilot>, IPilotService
{
    public PilotService(IPilotRepository pilots, ILogger<PilotService> logger) : base(pilots, logger) {}

    protected override void CopyValues(Pilot target, Pilot source)
    {
        target.Name = source.Name;
        target.WeightKg = source.WeightKg;
    }
}
