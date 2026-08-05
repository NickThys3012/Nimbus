using Nimbus.Contracts.DTOs.Features.Auth;
using Nimbus.Domain.Entities;
namespace Nimbus.Contracts.Mappers;

public static class AuthMappers
{
    public static UserDto MapToUserDto(this User user)
    {
        return new UserDto(
            user.Id,
            user.Email,
            user.FirstName,
            user.Name
        );
    }
}
