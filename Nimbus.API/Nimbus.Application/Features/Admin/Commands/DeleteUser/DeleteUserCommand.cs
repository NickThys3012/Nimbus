using MediatR;
namespace Nimbus.Application.Features.Admin.Commands.DeleteUser;

public sealed record DeleteUserCommand(Guid UserId) : IRequest;
