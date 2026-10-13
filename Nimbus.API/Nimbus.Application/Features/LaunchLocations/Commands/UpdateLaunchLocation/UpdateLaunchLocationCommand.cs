using MediatR;
using Nimbus.Contracts.DTOs.Features.LaunchLocations;
namespace Nimbus.Application.Features.LaunchLocations.Commands.UpdateLaunchLocation;

public sealed record UpdateLaunchLocationCommand(Guid Id, LaunchLocationRequestDto LaunchLocation) : IRequest<LaunchLocationDto>;
