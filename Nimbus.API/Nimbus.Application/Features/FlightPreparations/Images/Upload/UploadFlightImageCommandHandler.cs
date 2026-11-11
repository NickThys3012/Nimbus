using FluentValidation.Results;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nimbus.Application.Common.Exceptions;
using Nimbus.Application.Common.Interfaces;
using Nimbus.Contracts.DTOs.Features.FlightPreparations;
using Nimbus.Domain.Entities;
using Nimbus.Domain.Enums;
using Nimbus.Domain.Exceptions;
using Nimbus.Domain.Interfaces;
using Nimbus.Domain.ValueObjects;
namespace Nimbus.Application.Features.FlightPreparations.Images.Upload;

/// <summary>
///     Attaches an image to a flight (issue #51). Only the owner or an admin may. The file is read with a hard size cap,
///     must be an allowed type and must decode as a real image (the bytes decide, not the extension or the claimed type).
///     The original and a thumbnail go to the <c>flight-images</c> bucket and only their metadata to the database; if the row
///     cannot be saved the objects are removed again, so a failure leaves nothing behind.
/// </summary>
public sealed class UploadFlightImageCommandHandler : IRequestHandler<UploadFlightImageCommand, FlightImageDto>
{
    private readonly IFlightDetailReader _access;
    private readonly ICurrentUserService _currentUser;
    private readonly IFlightImageRepository _images;
    private readonly IImageInspector _inspector;
    private readonly ILogger<UploadFlightImageCommandHandler> _logger;
    private readonly ImageUploadOptions _options;
    private readonly IObjectStorageService _storage;

    public UploadFlightImageCommandHandler(
        IFlightDetailReader access, IFlightImageRepository images, IObjectStorageService storage, IImageInspector inspector,
        ICurrentUserService currentUser, IOptions<ImageUploadOptions> options, ILogger<UploadFlightImageCommandHandler> logger)
    {
        _access = access;
        _images = images;
        _storage = storage;
        _inspector = inspector;
        _currentUser = currentUser;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<FlightImageDto> Handle(UploadFlightImageCommand request, CancellationToken ct)
    {
        var me = _currentUser.UserId;
        var access = await _access.GetAccessAsync(me, _currentUser.IsAdmin, request.FlightId, ct);
        if (!access.Exists)
            throw new NotFoundException(nameof(FlightPreparation), request.FlightId);
        if (access.Role is not (FlightViewerRole.Owner or FlightViewerRole.Admin))
        {
            _logger.LogWarning(
                "Rejected an attempt by user {UserId} to add an image to flight {FlightId} (viewer role: {Role})",
                me, request.FlightId, access.Role?.ToString() ?? "none");
            throw new ForbiddenException(nameof(FlightPreparation), request.FlightId);
        }

        var declared = request.DeclaredContentType.Split(';')[0].Trim();
        if (!_options.AllowedContentTypes.Contains(declared, StringComparer.OrdinalIgnoreCase))
            throw Invalid($"Only {string.Join(", ", _options.AllowedContentTypes)} images can be uploaded.");

        var content = await ReadCappedAsync(request.Content, ct);
        var inspected = await _inspector.InspectAsync(content, ct);
        if (inspected is null || !_options.AllowedContentTypes.Contains(inspected.ContentType, StringComparer.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Rejected an upload to flight {FlightId} by user {UserId}: the file is not a supported image", request.FlightId, me);
            throw Invalid("The file is not a valid image.");
        }

        var owner = access.OwnerId!.Value;
        var id = Guid.NewGuid();
        var originalKey = ObjectKey.ForFlightAsset(owner, request.FlightId, $"{id:D}.{inspected.Extension}");
        var thumbnailKey = ObjectKey.ForFlightAsset(owner, request.FlightId, $"{id:D}.thumb.{inspected.Extension}");

        var image = new FlightPreparationImage
        {
            Id = id,
            FlightPreparationId = request.FlightId,
            Section = request.Section,
            ObjectKey = originalKey.Value,
            ThumbnailObjectKey = thumbnailKey.Value,
            ContentType = inspected.ContentType,
            SizeBytes = content.LongLength
        };

        var stored = new List<ObjectKey>();
        try
        {
            await UploadAsync(originalKey, content, inspected.ContentType, stored, ct);
            await UploadAsync(thumbnailKey, inspected.Thumbnail.Content, inspected.Thumbnail.ContentType, stored, ct);
            await _images.AddAsync(image, ct);
        }
        catch
        {
            await RemoveStoredAsync(stored, request.FlightId);
            throw;
        }

        _logger.LogInformation(
            "User {UserId} added image {ImageId} ({Bytes} bytes, {ContentType}) to the {Section} section of flight {FlightId}",
            me, id, image.SizeBytes, image.ContentType, image.Section, request.FlightId);

        return await FlightImageLinks.TryCreateAsync(_storage, image, _logger)
               ?? throw new ObjectStorageException($"Image {id} was stored but a download link could not be made for it.");
    }

    private async Task UploadAsync(ObjectKey key, byte[] bytes, string contentType, List<ObjectKey> stored, CancellationToken ct)
    {
        await using var stream = new MemoryStream(bytes, false);
        await _storage.UploadAsync(StorageBucket.FlightImages, key, new ObjectUpload(stream, contentType, bytes.LongLength), ct);
        stored.Add(key);
    }

    private async Task RemoveStoredAsync(List<ObjectKey> stored, Guid flightId)
    {
        foreach (var key in stored)
        {
            try
            {
                await _storage.DeleteAsync(StorageBucket.FlightImages, key);
            }
            catch (Exception ex) when (ex is ObjectStorageException)
            {
                _logger.LogWarning(ex, "Upload to flight {FlightId} failed and its file {Key} could not be removed again; it is orphaned", flightId, key);
            }
        }
    }

    private async Task<byte[]> ReadCappedAsync(Stream source, CancellationToken ct)
    {
        // Read at most one byte past the cap: enough to know it is too large without buffering an unbounded body.
        using var buffer = new MemoryStream();
        var copied = 0L;
        var chunk = new byte[81920];
        int read;
        while ((read = await source.ReadAsync(chunk, ct)) > 0)
        {
            copied += read;
            if (copied > _options.MaxBytes)
                throw Invalid($"The image is larger than the {_options.MaxBytes / (1024 * 1024)} MB limit.");
            buffer.Write(chunk, 0, read);
        }

        if (copied == 0)
            throw Invalid("The file is empty.");
        return buffer.ToArray();
    }

    private static ValidationException Invalid(string message)
    {
        return new ValidationException([new ValidationFailure("file", message)]);
    }
}
