using MediatR;
using Microsoft.Extensions.Logging;
using Nimbus.Application.Common.Exceptions;
using Nimbus.Application.Common.Interfaces;
using Nimbus.Domain.Entities;
using Nimbus.Domain.Enums;
using Nimbus.Domain.Exceptions;
using Nimbus.Domain.Interfaces;
using Nimbus.Domain.ValueObjects;
namespace Nimbus.Application.Features.FlightPreparations.Commands.DeleteFlightPreparation;

/// <summary>
///     Deletes a flight for good (issue #43). Only the owner or an admin may; anyone else who can see the flight is
///     rejected with 403 and the attempt is logged. The database removes the passengers, wind levels, images and shares;
///     the image and KML files in object storage are then removed best effort, because the row is the source of truth and
///     a storage hiccup must neither undo nor block the delete (an orphaned file is logged).
/// </summary>
public sealed class DeleteFlightPreparationCommandHandler : IRequestHandler<DeleteFlightPreparationCommand>
{
    private readonly IFlightDetailReader _access;
    private readonly ICurrentUserService _currentUser;
    private readonly IFlightPreparationRepository _flights;
    private readonly ILogger<DeleteFlightPreparationCommandHandler> _logger;
    private readonly IObjectStorageService _storage;

    public DeleteFlightPreparationCommandHandler(
        IFlightDetailReader access, IFlightPreparationRepository flights, IObjectStorageService storage,
        ICurrentUserService currentUser, ILogger<DeleteFlightPreparationCommandHandler> logger)
    {
        _access = access;
        _flights = flights;
        _storage = storage;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task Handle(DeleteFlightPreparationCommand request, CancellationToken ct)
    {
        var me = _currentUser.UserId;
        var access = await _access.GetAccessAsync(me, _currentUser.IsAdmin, request.Id, ct);
        if (!access.Exists)
            throw new NotFoundException(nameof(FlightPreparation), request.Id);

        if (access.Role is not (FlightViewerRole.Owner or FlightViewerRole.Admin))
        {
            _logger.LogWarning(
                "Rejected an attempt by user {UserId} to delete flight {FlightId} (viewer role: {Role})",
                me, request.Id, access.Role?.ToString() ?? "none");
            throw new ForbiddenException(nameof(FlightPreparation), request.Id);
        }

        // The repository is owner-scoped; an admin deletes on the owner's behalf, so it is asked as the owner.
        var owner = access.OwnerId!.Value;
        var deletion = await _flights.DeleteAsync(owner, request.Id, ct)
                       ?? throw new NotFoundException(nameof(FlightPreparation), request.Id);

        _logger.LogInformation(
            "Deleted flight {FlightId} of owner {OwnerId} by user {UserId} ({Role}); {ImageCount} image file(s) and {KmlCount} track file(s) to remove",
            request.Id, owner, me, access.Role, deletion.ImageObjectKeys.Count, deletion.KmlTrackObjectKey is null ? 0 : 1);

        foreach (var key in deletion.ImageObjectKeys)
            await RemoveFileAsync(StorageBucket.FlightImages, key, request.Id);
        if (deletion.KmlTrackObjectKey is { } kml)
            await RemoveFileAsync(StorageBucket.FlightTracks, kml, request.Id);
    }

    private async Task RemoveFileAsync(StorageBucket bucket, string key, Guid flightId)
    {
        try
        {
            await _storage.DeleteAsync(bucket, ObjectKey.ForSharedAsset(key));
        }
        catch (Exception ex) when (ex is ObjectStorageException or DomainException)
        {
            _logger.LogWarning(ex, "Flight {FlightId} is deleted but its file {Key} in {Bucket} could not be removed; it is orphaned", flightId, key, bucket);
        }
    }
}
