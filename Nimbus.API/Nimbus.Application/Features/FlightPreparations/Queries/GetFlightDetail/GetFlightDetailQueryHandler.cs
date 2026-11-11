using MediatR;
using Microsoft.Extensions.Logging;
using Nimbus.Application.Common.Exceptions;
using Nimbus.Application.Common.Interfaces;
using Nimbus.Application.Features.FlightAssessment;
using Nimbus.Application.Features.FlightPreparations.Images;
using Nimbus.Contracts.DTOs.Features.FlightPreparations;
using Nimbus.Contracts.Mappers;
using Nimbus.Domain.Entities;
using Nimbus.Domain.Enums;
using Nimbus.Domain.Exceptions;
using Nimbus.Domain.Interfaces;
using Nimbus.Domain.ValueObjects;
namespace Nimbus.Application.Features.FlightPreparations.Queries.GetFlightDetail;

public sealed class GetFlightDetailQueryHandler : IRequestHandler<GetFlightDetailQuery, FlightDetailDto>
{
    private readonly IFlightAssessmentService _assessment;
    private readonly ICurrentUserService _currentUser;
    private readonly IUserDirectory _directory;
    private readonly ILogger<GetFlightDetailQueryHandler> _logger;
    private readonly IFlightDetailReader _reader;
    private readonly IObjectStorageService _storage;

    public GetFlightDetailQueryHandler(
        IFlightDetailReader reader, IFlightAssessmentService assessment, IUserDirectory directory,
        IObjectStorageService storage, ICurrentUserService currentUser, ILogger<GetFlightDetailQueryHandler> logger)
    {
        _reader = reader;
        _assessment = assessment;
        _directory = directory;
        _storage = storage;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<FlightDetailDto> Handle(GetFlightDetailQuery request, CancellationToken ct)
    {
        var result = await _reader.GetAsync(_currentUser.UserId, _currentUser.IsAdmin, request.Id, ct);
        if (!result.Access.Exists)
            throw new NotFoundException(nameof(FlightPreparation), request.Id);
        // The decision is the reader's, made server side: a hidden button is never the only barrier.
        if (result.Data is not { } data)
            throw new ForbiddenException(nameof(FlightPreparation), request.Id);

        var flight = data.Flight;
        var assessment = await _assessment.AssessAsync(data.Assessment, ct);
        var ownerName = data.Role == FlightViewerRole.Owner
            ? null
            : (await _directory.GetDisplayNamesAsync([flight.OwnerId], ct)).GetValueOrDefault(flight.OwnerId);

        return new FlightDetailDto(
            flight.Id, flight.Status.ToString(), flight.FlownAt, data.Role.ToString(), FlightPermissions.For(data.Role), ownerName,
            data.BalloonRegistration, data.BalloonType, data.PilotName, data.LocationName, data.LocationIcaoCode, assessment,
            flight.MapToRequestDto(), await ImagesAsync(flight));
    }

    private async Task<IReadOnlyList<FlightImageDto>> ImagesAsync(FlightPreparation flight)
    {
        var images = new List<FlightImageDto>();
        foreach (var image in flight.Images.OrderBy(i => i.Order))
        {
            // A storage hiccup or a malformed key must not take the whole view down; the image is simply omitted.
            if (await FlightImageLinks.TryCreateAsync(_storage, image, _logger) is { } dto)
                images.Add(dto);
        }

        return images;
    }
}
