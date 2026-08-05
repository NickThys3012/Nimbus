using MediatR;
using Nimbus.Contracts.DTOs.Features.Auth;
namespace Nimbus.Application.Features.Auth.Queries.GetUserByEmail;

public record GetUserByEmailQuery(string Email) : IRequest<UserDto>;
