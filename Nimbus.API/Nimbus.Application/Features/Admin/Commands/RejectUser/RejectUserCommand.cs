using MediatR;
namespace Nimbus.Application.Features.Admin.Commands.RejectUser;

public sealed record RejectUserCommand(Guid UserId) : IRequest;
