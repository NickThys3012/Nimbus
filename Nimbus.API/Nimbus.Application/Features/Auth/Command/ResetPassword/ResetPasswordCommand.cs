using MediatR;
namespace Nimbus.Application.Features.Auth.Command.ResetPassword;

public sealed record ResetPasswordCommand(string Email, string Token, string NewPassword) : IRequest<bool>;
