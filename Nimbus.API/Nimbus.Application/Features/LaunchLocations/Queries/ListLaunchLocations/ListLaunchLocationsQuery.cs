using MediatR;
using Nimbus.Contracts.DTOs.Features.LaunchLocations;
namespace Nimbus.Application.Features.LaunchLocations.Queries.ListLaunchLocations;

public sealed record ListLaunchLocationsQuery : IRequest<IReadOnlyList<LaunchLocationDto>>;
