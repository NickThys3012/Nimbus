using MediatR;
namespace Nimbus.Application.Features.Auth.Command.ForgotPassword;

public sealed record ForgotPasswordCommand(string Email, string WebBaseUrl) : IRequest;
