using MediatR;
using Nimbus.Contracts.DTOs.Features.Auth.Register;
namespace Nimbus.Application.Features.Auth.Command.CreateUser;

public sealed record CreateUserCommand(RegisterRequestDto Request, string BaseUrl) : IRequest<string>;
