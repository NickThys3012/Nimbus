using MediatR;
namespace Nimbus.Application.Features.Admin.Commands.SetUserRole;

public sealed record SetUserRoleCommand(Guid UserId, string Role) : IRequest;
