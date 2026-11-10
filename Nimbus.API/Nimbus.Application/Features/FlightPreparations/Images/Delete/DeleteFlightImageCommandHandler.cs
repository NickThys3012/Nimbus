using MediatR;
using Microsoft.Extensions.Logging;
using Nimbus.Application.Common.Exceptions;
using Nimbus.Application.Common.Interfaces;
using Nimbus.Domain.Entities;
using Nimbus.Domain.Enums;
using Nimbus.Domain.Interfaces;
using Nimbus.Domain.ValueObjects;
namespace Nimbus.Application.Features.FlightPreparations.Images.Delete;

/// <summary>
///     Removes one image (issue #51): owner or admin only. The files go first, then the row, and a failure of either is
///     thrown to the caller instead of being swallowed. Because the files are removed before the row, a failure never leaves a
///     row pointing at nothing silently: the row stays, the request fails, and repeating it finishes the job (deleting a file
///     that is already gone is not an error).
/// </summary>
public sealed class DeleteFlightImageCommandHandler : IRequestHandler<DeleteFlightImageCommand>
{
    private readonly IFlightDetailReader _access;
    private readonly ICurrentUserService _currentUser;
    private readonly IFlightImageRepository _images;
    private readonly ILogger<DeleteFlightImageCommandHandler> _logger;
    private readonly IObjectStorageService _storage;

    public DeleteFlightImageCommandHandler(
        IFlightDetailReader access, IFlightImageRepository images, IObjectStorageService storage,
        ICurrentUserService currentUser, ILogger<DeleteFlightImageCommandHandler> logger)
    {
        _access = access;
        _images = images;
        _storage = storage;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task Handle(DeleteFlightImageCommand request, CancellationToken ct)
    {
        var me = _currentUser.UserId;
        var access = await _access.GetAccessAsync(me, _currentUser.IsAdmin, request.FlightId, ct);
        if (!access.Exists)
            throw new NotFoundException(nameof(FlightPreparation), request.FlightId);
        if (access.Role is not (FlightViewerRole.Owner or FlightViewerRole.Admin))
        {
            _logger.LogWarning(
                "Rejected an attempt by user {UserId} to delete image {ImageId} of flight {FlightId} (viewer role: {Role})",
                me, request.ImageId, request.FlightId, access.Role?.ToString() ?? "none");
            throw new ForbiddenException(nameof(FlightPreparation), request.FlightId);
        }

        var image = await _images.GetAsync(request.FlightId, request.ImageId, ct)
                    ?? throw new NotFoundException(nameof(FlightPreparationImage), request.ImageId);

        // Storage errors propagate (ObjectStorageException) with the row untouched.
        await _storage.DeleteAsync(StorageBucket.FlightImages, ObjectKey.ForSharedAsset(image.ObjectKey), ct);
        if (!string.IsNullOrWhiteSpace(image.ThumbnailObjectKey))
            await _storage.DeleteAsync(StorageBucket.FlightImages, ObjectKey.ForSharedAsset(image.ThumbnailObjectKey), ct);

        try
        {
            await _images.DeleteAsync(request.FlightId, request.ImageId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Image {ImageId} of flight {FlightId}: its files are removed but the database row could not be; retry the delete", request.ImageId, request.FlightId);
            throw;
        }

        _logger.LogInformation("User {UserId} deleted image {ImageId} of flight {FlightId}", me, request.ImageId, request.FlightId);
    }
}
