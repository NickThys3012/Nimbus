namespace Nimbus.Contracts.DTOs.Features.Auth;

public record UserDto(
    Guid Id,
    string Email,
    string FirstName,
    string LastName
);
