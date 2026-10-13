using Microsoft.Extensions.Logging;
using Nimbus.Application.Common.Ownership;
using Nimbus.Domain.Entities;
using Nimbus.Domain.Interfaces;
namespace Nimbus.Application.Features.LaunchLocations;

public interface ILaunchLocationService : IOwnedEntityService<LaunchLocation> {}

/// <summary>Owner-scoped launch location operations (see docs/launch-locations.md).</summary>
public sealed class LaunchLocationService : OwnedEntityService<LaunchLocation>, ILaunchLocationService
{
    public LaunchLocationService(ILaunchLocationRepository launchLocations, ILogger<LaunchLocationService> logger) : base(launchLocations, logger) {}

    protected override void CopyValues(LaunchLocation target, LaunchLocation source)
    {
        target.Name = source.Name;
        target.IcaoCode = source.IcaoCode;
        target.Latitude = source.Latitude;
        target.Longitude = source.Longitude;
        target.ElevationM = source.ElevationM;
        target.AirspaceNotes = source.AirspaceNotes;
    }
}
