using Nimbus.Contracts.DTOs.Features.FlightPreparations;
using Nimbus.Domain.Interfaces;
namespace Nimbus.Application.Features.FlightPreparations;

/// <summary>
///     Who may do what with a flight. Reading and exporting is open to the owner, an admin and anyone it is shared with.
///     Everything that changes the flight (edit, share on, mark as flown, delete) belongs to the owner and to an admin,
///     who has the owner's full set (the flight-detail design shows an admin every owner action). A viewer a flight is
///     merely shared with can read and export, nothing else.
/// </summary>
public static class FlightPermissions
{
    public static FlightPermissionsDto For(FlightViewerRole role)
    {
        var manages = role is FlightViewerRole.Owner or FlightViewerRole.Admin;
        return new FlightPermissionsDto(CanEdit: manages, CanDelete: manages, CanShare: manages, CanMarkFlown: manages, CanExport: true);
    }
}
