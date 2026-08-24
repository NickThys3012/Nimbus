namespace Nimbus.Contracts.DTOs.Features.Auth.Register;

public record RegisterRequestDto(string Email, string Password, string FirstName, string LastName);
