using Nimbus.Contracts.DTOs.Features.FlightPreparations;
using Nimbus.Domain.Interfaces;
namespace Nimbus.Application.Features.FlightPreparations;

/// <summary>
///     Who may do what with a flight. Reading and exporting is open to the owner, an admin and anyone it is shared with.
///     Everything that changes the flight (edit, share on, mark as flown, delete) is the owner's alone; an admin sees
///     everything but does not edit someone else's dossier.
/// </summary>
public static class FlightPermissions
{
    public static FlightPermissionsDto For(FlightViewerRole role)
    {
        var owner = role == FlightViewerRole.Owner;
        return new FlightPermissionsDto(CanEdit: owner, CanDelete: owner, CanShare: owner, CanMarkFlown: owner, CanExport: true);
    }
}
