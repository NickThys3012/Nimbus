using MediatR;
using Microsoft.Extensions.Logging;
using Nimbus.Application.Common.Exceptions;
using Nimbus.Application.Common.Interfaces;
using Nimbus.Contracts.DTOs.Features.FlightPreparations;
using Nimbus.Domain.Entities;
using Nimbus.Domain.Interfaces;
namespace Nimbus.Application.Features.FlightPreparations.Images.List;

public sealed class ListFlightImagesQueryHandler : IRequestHandler<ListFlightImagesQuery, IReadOnlyList<FlightImageDto>>
{
    private readonly IFlightDetailReader _access;
    private readonly ICurrentUserService _currentUser;
    private readonly IFlightImageRepository _images;
    private readonly ILogger<ListFlightImagesQueryHandler> _logger;
    private readonly IObjectStorageService _storage;

    public ListFlightImagesQueryHandler(
        IFlightDetailReader access, IFlightImageRepository images, IObjectStorageService storage, ICurrentUserService currentUser,
        ILogger<ListFlightImagesQueryHandler> logger)
    {
        _access = access;
        _images = images;
        _storage = storage;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<IReadOnlyList<FlightImageDto>> Handle(ListFlightImagesQuery request, CancellationToken ct)
    {
        var access = await _access.GetAccessAsync(_currentUser.UserId, _currentUser.IsAdmin, request.FlightId, ct);
        if (!access.Exists)
            throw new NotFoundException(nameof(FlightPreparation), request.FlightId);
        if (access.Role is null)
            throw new ForbiddenException(nameof(FlightPreparation), request.FlightId);

        var result = new List<FlightImageDto>();
        foreach (var image in await _images.ListAsync(request.FlightId, ct))
        {
            if (await FlightImageLinks.TryCreateAsync(_storage, image, _logger) is { } dto)
                result.Add(dto);
        }

        return result;
    }
}
