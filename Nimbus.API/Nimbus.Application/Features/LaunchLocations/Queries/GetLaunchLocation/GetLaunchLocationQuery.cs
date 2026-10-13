using MediatR;
using Nimbus.Contracts.DTOs.Features.LaunchLocations;
namespace Nimbus.Application.Features.LaunchLocations.Queries.GetLaunchLocation;

public sealed record GetLaunchLocationQuery(Guid Id) : IRequest<LaunchLocationDto>;
