using MediatR;
namespace Nimbus.Application.Features.Admin.Commands.SetUserBlocked;

/// <summary>Blocks (indefinite lockout, sessions revoked) or unblocks a user account.</summary>
public sealed record SetUserBlockedCommand(Guid UserId, bool Blocked) : IRequest;
