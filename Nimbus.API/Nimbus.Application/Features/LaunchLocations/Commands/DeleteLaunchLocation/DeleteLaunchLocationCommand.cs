using MediatR;
namespace Nimbus.Application.Features.LaunchLocations.Commands.DeleteLaunchLocation;

/// <summary>Soft delete of one of the caller's own launch locations.</summary>
public sealed record DeleteLaunchLocationCommand(Guid Id) : IRequest;
