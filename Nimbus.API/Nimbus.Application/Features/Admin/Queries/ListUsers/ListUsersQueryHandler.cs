using MediatR;
using Nimbus.Contracts.DTOs.Features.Admin;
using Nimbus.Contracts.Mappers;
using Nimbus.Domain.Interfaces;
namespace Nimbus.Application.Features.Admin.Queries.ListUsers;

public sealed class ListUsersQueryHandler : IRequestHandler<ListUsersQuery, IReadOnlyList<AdminUserDto>>
{
    private readonly IUserRepository _userRepository;

    public ListUsersQueryHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<IReadOnlyList<AdminUserDto>> Handle(ListUsersQuery request, CancellationToken ct)
    {
        var users = await _userRepository.ListAllAsync(ct);
        return users.Select(u => u.MapToAdminUserDto()).ToList();
    }
}
