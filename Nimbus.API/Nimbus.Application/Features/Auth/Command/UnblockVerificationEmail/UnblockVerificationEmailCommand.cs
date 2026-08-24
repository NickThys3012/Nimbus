using MediatR;
namespace Nimbus.Application.Features.Auth.Command.UnblockVerificationEmail;

public sealed record UnblockVerificationEmailCommand(string Email) : IRequest;
