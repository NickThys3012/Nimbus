using MediatR;
using Nimbus.Contracts.DTOs.Features.LaunchLocations;
namespace Nimbus.Application.Features.LaunchLocations.Commands.CreateLaunchLocation;

public sealed record CreateLaunchLocationCommand(LaunchLocationRequestDto LaunchLocation) : IRequest<LaunchLocationDto>;
