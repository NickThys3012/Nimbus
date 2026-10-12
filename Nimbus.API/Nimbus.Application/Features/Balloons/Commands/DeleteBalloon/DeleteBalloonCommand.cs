using MediatR;
namespace Nimbus.Application.Features.Balloons.Commands.DeleteBalloon;

/// <summary>Soft delete, blocked (409) while a flight uses the balloon (docs/balloons.md).</summary>
public sealed record DeleteBalloonCommand(Guid Id) : IRequest;
