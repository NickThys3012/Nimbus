using MediatR;
using Nimbus.Contracts.DTOs.Features.Auth;

namespace Nimbus.Application.Features.Auth.Command.ResendVerificationEmail;

public sealed record ResendVerificationEmailCommand(ResendVerificationEmailCommandDto Command, string BaseUrl) : IRequest;
