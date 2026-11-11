using Microsoft.Extensions.Logging;
using Nimbus.Contracts.DTOs.Features.FlightPreparations;
using Nimbus.Domain.Entities;
using Nimbus.Domain.Enums;
using Nimbus.Domain.Exceptions;
using Nimbus.Domain.Interfaces;
using Nimbus.Domain.ValueObjects;
namespace Nimbus.Application.Features.FlightPreparations.Images;

/// <summary>Turns an image row into the DTO the client sees, with short-lived presigned links (never a permanent URL).</summary>
public static class FlightImageLinks
{
    /// <summary>Null when the links cannot be made (storage down, malformed key): the caller omits the image instead of failing the view.</summary>
    public static async Task<FlightImageDto?> TryCreateAsync(
        IObjectStorageService storage, FlightPreparationImage image, ILogger logger)
    {
        try
        {
            var url = await Link(storage, image.ObjectKey);
            var thumb = string.IsNullOrWhiteSpace(image.ThumbnailObjectKey) ? url : await Link(storage, image.ThumbnailObjectKey);
            return new FlightImageDto(
                image.Id, image.Section.ToString(), image.ContentType, url, thumb, image.SizeBytes, image.Order, image.CreatedDate);
        }
        catch (Exception ex) when (ex is ObjectStorageException or DomainException)
        {
            logger.LogWarning(ex, "Could not link image {ImageId} of flight {FlightId}; omitting it", image.Id, image.FlightPreparationId);
            return null;
        }
    }

    private static async Task<string> Link(IObjectStorageService storage, string key)
    {
        return (await storage.GetPresignedDownloadUrlAsync(StorageBucket.FlightImages, ObjectKey.ForSharedAsset(key))).ToString();
    }
}
