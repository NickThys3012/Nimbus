using MediatR;
namespace Nimbus.Application.Features.Admin.Commands.ApproveUser;

public sealed record ApproveUserCommand(Guid UserId) : IRequest;
