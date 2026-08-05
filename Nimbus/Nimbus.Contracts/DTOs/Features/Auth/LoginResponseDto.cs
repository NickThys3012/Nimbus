namespace Nimbus.Contracts.DTOs.Features.Auth;

public record LoginResponseDto(string AccessToken, DateTime Expiration, string Email, string Role);
