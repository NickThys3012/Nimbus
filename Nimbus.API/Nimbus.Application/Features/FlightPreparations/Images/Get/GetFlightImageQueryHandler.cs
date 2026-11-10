using MediatR;
using Nimbus.Application.Common.Exceptions;
using Nimbus.Application.Common.Interfaces;
using Nimbus.Domain.Entities;
using Nimbus.Domain.Enums;
using Nimbus.Domain.Interfaces;
using Nimbus.Domain.ValueObjects;
namespace Nimbus.Application.Features.FlightPreparations.Images.Get;

/// <summary>
///     Streams an image through the API (issue #51), so the browser never needs a storage address. Anyone who may see the
///     flight (owner, admin, an active share) may read it; everyone else gets 403, a missing flight or image 404. Access is
///     checked on every request, so a link copied out of the app stops working when the share is revoked.
/// </summary>
public sealed class GetFlightImageQueryHandler : IRequestHandler<GetFlightImageQuery, ObjectDownload>
{
    private readonly IFlightDetailReader _access;
    private readonly ICurrentUserService _currentUser;
    private readonly IFlightImageRepository _images;
    private readonly IObjectStorageService _storage;

    public GetFlightImageQueryHandler(
        IFlightDetailReader access, IFlightImageRepository images, IObjectStorageService storage, ICurrentUserService currentUser)
    {
        _access = access;
        _images = images;
        _storage = storage;
        _currentUser = currentUser;
    }

    public async Task<ObjectDownload> Handle(GetFlightImageQuery request, CancellationToken ct)
    {
        var access = await _access.GetAccessAsync(_currentUser.UserId, _currentUser.IsAdmin, request.FlightId, ct);
        if (!access.Exists)
            throw new NotFoundException(nameof(FlightPreparation), request.FlightId);
        if (access.Role is null)
            throw new ForbiddenException(nameof(FlightPreparation), request.FlightId);

        var image = await _images.GetAsync(request.FlightId, request.ImageId, ct)
                    ?? throw new NotFoundException(nameof(FlightPreparationImage), request.ImageId);

        // Images that predate thumbnails fall back to the original.
        var key = request.Thumbnail && !string.IsNullOrWhiteSpace(image.ThumbnailObjectKey) ? image.ThumbnailObjectKey : image.ObjectKey;
        return await _storage.DownloadAsync(StorageBucket.FlightImages, ObjectKey.ForSharedAsset(key), ct)
               ?? throw new NotFoundException(nameof(FlightPreparationImage), request.ImageId);
    }
}
