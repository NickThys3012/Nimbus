using FluentValidation.Results;
using MediatR;
using Microsoft.Extensions.Logging;
using Nimbus.Application.Common.Exceptions;
using Nimbus.Application.Common.Interfaces;
using Nimbus.Domain.Entities;
using Nimbus.Domain.Interfaces;
namespace Nimbus.Application.Features.FlightPreparations.Images.Reorder;

/// <summary>
///     Sets the display order of one section's images. Owner or admin only. The request must name every image of the section
///     exactly once, so a stale client (an image added or removed meanwhile) is told so instead of silently dropping or
///     misplacing one.
/// </summary>
public sealed class ReorderFlightImagesCommandHandler : IRequestHandler<ReorderFlightImagesCommand>
{
    private readonly IFlightDetailReader _access;
    private readonly ICurrentUserService _currentUser;
    private readonly IFlightImageRepository _images;
    private readonly ILogger<ReorderFlightImagesCommandHandler> _logger;

    public ReorderFlightImagesCommandHandler(
        IFlightDetailReader access, IFlightImageRepository images, ICurrentUserService currentUser, ILogger<ReorderFlightImagesCommandHandler> logger)
    {
        _access = access;
        _images = images;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task Handle(ReorderFlightImagesCommand request, CancellationToken ct)
    {
        var me = _currentUser.UserId;
        var access = await _access.GetAccessAsync(me, _currentUser.IsAdmin, request.FlightId, ct);
        if (!access.Exists)
            throw new NotFoundException(nameof(FlightPreparation), request.FlightId);
        if (access.Role is not (FlightViewerRole.Owner or FlightViewerRole.Admin))
        {
            _logger.LogWarning(
                "Rejected an attempt by user {UserId} to reorder the images of flight {FlightId} (viewer role: {Role})",
                me, request.FlightId, access.Role?.ToString() ?? "none");
            throw new ForbiddenException(nameof(FlightPreparation), request.FlightId);
        }

        var current = (await _images.ListAsync(request.FlightId, ct)).Where(i => i.Section == request.Section).Select(i => i.Id).ToHashSet();
        if (request.ImageIds.Count != current.Count || !current.SetEquals(request.ImageIds))
            throw new ValidationException([new ValidationFailure("imageIds", "The list must contain every image of the section exactly once; reload and try again.")]);

        await _images.SetOrderAsync(request.FlightId, request.ImageIds, ct);
    }
}
