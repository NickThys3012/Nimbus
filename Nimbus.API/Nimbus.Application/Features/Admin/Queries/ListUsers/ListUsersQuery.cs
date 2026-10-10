using MediatR;
using Nimbus.Contracts.DTOs.Features.Admin;
namespace Nimbus.Application.Features.Admin.Queries.ListUsers;

/// <summary>Admin-only (enforced by the API policy): lists every user with approval state and roles.</summary>
public sealed record ListUsersQuery : IRequest<IReadOnlyList<AdminUserDto>>;
