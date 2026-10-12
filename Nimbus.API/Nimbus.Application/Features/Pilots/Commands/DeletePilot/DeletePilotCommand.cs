using MediatR;
namespace Nimbus.Application.Features.Pilots.Commands.DeletePilot;

/// <summary>Soft delete of one of the caller's own pilots.</summary>
public sealed record DeletePilotCommand(Guid Id) : IRequest;
